using System.Data.Common;
using CrmObras.Infraestrutura.Mensagens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;

/// <summary>
/// A caixa de saída do lado de quem a processa: reservar a próxima mensagem,
/// marcá-la como tratada, ou registrar a falha para tentar de novo mais
/// tarde (ADR-0016).
/// </summary>
/// <remarks>
/// <para>
/// A reserva é a única leitura que atravessa construtoras, e passa por uma
/// função do banco que entrega só o id e a construtora da próxima mensagem —
/// e a trava, com <c>for update skip locked</c>, até o fim da transação. Dali
/// em diante, o trabalho é em nome da construtora da mensagem: o conteúdo
/// dela, os agregados que o tratamento lê e o que ele grava passam pelo
/// filtro do código e pela segurança por linha, como qualquer pedido
/// (ADR-0011).
/// </para>
/// <para>
/// Dois processadores nunca pegam a mesma mensagem: o segundo pula a que está
/// travada e pega a seguinte.
/// </para>
/// </remarks>
public sealed class FilaDaCaixaDeSaida(ContextoDoBanco contexto, ConstrutoraAssumida construtora, TimeProvider relogio)
{
    /// <summary>Depois de tantas falhas, a fila desiste da mensagem — e ela fica abandonada, para alguém olhar.</summary>
    public const int TentativasAntesDeDesistir = 10;

    private static readonly TimeSpan PrimeiraEspera = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan MaiorEspera = TimeSpan.FromHours(1);

    /// <summary>
    /// A espera depois da tentativa que falhou: 30 segundos, e o dobro a cada
    /// falha, até uma hora. Uma falha passageira — o planejamento reiniciando
    /// — se resolve logo; uma persistente não ocupa a fila.
    /// </summary>
    public static TimeSpan Espera(int tentativas) =>
        tentativas <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(Math.Min(MaiorEspera.Ticks, PrimeiraEspera.Ticks * (1L << Math.Min(tentativas - 1, 20))));

    /// <summary>
    /// Reserva a próxima mensagem pendente, na transação em curso, e assume a
    /// construtora dela.
    /// </summary>
    /// <returns><see langword="null"/> se não há mensagem pendente.</returns>
    public async Task<MensagemReservada?> ReservarProxima(CancellationToken cancelamento = default)
    {
        var transacao = contexto.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A reserva vale até o fim da transação; abra uma antes de reservar.");

        if (await Reservar(transacao, relogio.GetUtcNow(), cancelamento) is not { } reservada)
        {
            return null;
        }

        var (id, daMensagem) = reservada;
        construtora.Assumir(daMensagem);

        // Só nesta transação: depois dela, a conexão volta a não ter construtora.
        await contexto.Database.ExecuteSqlAsync(
            $"select set_config('crm.construtora_id', {daMensagem.ToString()}, true)",
            cancelamento);

        var registro = await contexto.CaixaDeSaida.AsNoTracking().SingleAsync(mensagem => mensagem.Id == id, cancelamento);

        return new MensagemReservada(
            registro.Id,
            registro.Tipo,
            registro.Formato,
            registro.Conteudo,
            registro.Tentativas,
            registro.ConstrutoraId,
            registro.OcorridaEm,
            registro.ContextoDoRastreio);
    }

    public async Task MarcarTratada(Guid mensagem, CancellationToken cancelamento = default)
    {
        var agora = relogio.GetUtcNow().ToUniversalTime();

        await contexto.CaixaDeSaida
            .Where(registro => registro.Id == mensagem)
            .ExecuteUpdateAsync(colunas => colunas.SetProperty(registro => registro.TratadaEm, agora), cancelamento);
    }

    /// <summary>Conta a tentativa, guarda o erro e marca quando tentar de novo — ou desiste, se já foram tentativas demais.</summary>
    /// <returns>Se a fila desistiu da mensagem.</returns>
    public async Task<bool> RegistrarFalha(MensagemReservada mensagem, Exception erro, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(erro);

        var agora = relogio.GetUtcNow().ToUniversalTime();
        var tentativas = mensagem.Tentativas + 1;
        DateTimeOffset? abandonadaEm = tentativas >= TentativasAntesDeDesistir ? agora : null;
        var ultimoErro = $"{erro.GetType().Name}: {erro.Message}";

        await contexto.CaixaDeSaida
            .Where(registro => registro.Id == mensagem.Id)
            .ExecuteUpdateAsync(
                colunas => colunas
                    .SetProperty(registro => registro.Tentativas, tentativas)
                    .SetProperty(registro => registro.ProximaTentativaEm, agora + Espera(tentativas))
                    .SetProperty(registro => registro.UltimoErro, ultimoErro)
                    .SetProperty(registro => registro.AbandonadaEm, abandonadaEm),
                cancelamento);

        return abandonadaEm is not null;
    }

    private async Task<(Guid Id, Guid Construtora)?> Reservar(IDbContextTransaction transacao, DateTimeOffset agora, CancellationToken cancelamento)
    {
        var conexao = contexto.Database.GetDbConnection();

        await using var comando = conexao.CreateCommand();
        comando.Transaction = transacao.GetDbTransaction();
        comando.CommandText = "select id, construtora_id from crm.reservar_mensagens(1, @agora)";
        Parametro(comando, "agora", agora.ToUniversalTime());

        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        return await leitor.ReadAsync(cancelamento) ? (leitor.GetGuid(0), leitor.GetGuid(1)) : null;
    }

    private static void Parametro(DbCommand comando, string nome, object valor)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }
}

/// <summary>Uma mensagem reservada para tratamento, já em nome da construtora dela.</summary>
/// <param name="OcorridaEm">Quando o fato foi gravado: a espera da mensagem conta daí.</param>
/// <param name="ContextoDoRastreio">O rastro de quem publicou, no formato <c>traceparent</c>; nulo se ninguém rastreava.</param>
public sealed record MensagemReservada(
    Guid Id,
    string Tipo,
    short Formato,
    string Conteudo,
    int Tentativas,
    Guid Construtora,
    DateTimeOffset OcorridaEm,
    string? ContextoDoRastreio)
{
    /// <summary>A mensagem no tipo dela — o que os tratadores recebem.</summary>
    public object Ler() => CatalogoDeMensagens.Ler(Tipo, Formato, Conteudo);
}
