using Microsoft.EntityFrameworkCore;

namespace CrmObras.Infraestrutura.Persistencia.Idempotencia;

/// <summary>
/// As chaves de idempotência de cada construtora, com a resposta dada a cada
/// uma (ADR-0013).
/// </summary>
/// <remarks>
/// <para>
/// A reserva da chave anda na mesma transação da operação que ela protege:
/// ou as duas ficam gravadas, ou nenhuma. Operação que falha desfaz a
/// reserva, e o pedido pode ser repetido com a mesma chave.
/// </para>
/// <para>
/// Um pedido repetido enquanto o primeiro ainda roda não executa de novo:
/// a reserva dele espera no índice único da chave até o primeiro terminar,
/// e então recebe a resposta guardada. É o PostgreSQL que põe os dois em
/// fila — não há janela entre conferir a chave e reservá-la.
/// </para>
/// </remarks>
public sealed class ChavesDeIdempotencia(ContextoDoBanco contexto, TimeProvider relogio)
{
    public const int TamanhoMaximoDaChave = 255;

    /// <summary>
    /// Atende o pedido uma vez só. Chave nova: executa a operação e, se ela
    /// deu certo, guarda a resposta. Chave já atendida: devolve a resposta
    /// guardada, sem executar nada.
    /// </summary>
    /// <param name="impressaoDoPedido">O que identifica o pedido, para reconhecê-lo quando ele chegar de novo.</param>
    /// <param name="operacao">
    /// A operação. Ela grava pelo mesmo contexto — o da requisição —, e por
    /// isso dentro da transação da chave.
    /// </param>
    /// <exception cref="ChaveDeIdempotenciaReutilizada">A chave já atendeu outro pedido.</exception>
    public async Task<PedidoAtendido> Atender(
        string chave,
        string impressaoDoPedido,
        Func<CancellationToken, Task<RespostaGuardada>> operacao,
        CancellationToken cancelamento = default)
    {
        Validar(chave);
        ArgumentException.ThrowIfNullOrWhiteSpace(impressaoDoPedido);
        ArgumentNullException.ThrowIfNull(operacao);

        await using var transacao = await contexto.Database.BeginTransactionAsync(cancelamento);

        var guardada = await Reservar(chave, impressaoDoPedido, cancelamento);

        if (guardada is not null)
        {
            return new PedidoAtendido(guardada, Repetido: true);
        }

        var resposta = await operacao(cancelamento);

        // Só o sucesso fica. Erro desfaz a operação e a reserva juntas, e o
        // pedido pode ser feito de novo com a mesma chave.
        if (resposta.Status is >= 200 and < 300)
        {
            await Guardar(chave, resposta, cancelamento);
            await transacao.CommitAsync(cancelamento);
        }

        return new PedidoAtendido(resposta, Repetido: false);
    }

    /// <summary>Reserva a chave para o pedido, na transação em curso.</summary>
    /// <returns><see langword="null"/> se a chave é nova; a resposta guardada, se este mesmo pedido já foi atendido.</returns>
    internal async Task<RespostaGuardada?> Reservar(string chave, string impressaoDoPedido, CancellationToken cancelamento)
    {
        if (contexto.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A chave de idempotência se reserva na mesma transação da operação que ela protege; abra a transação antes.");
        }

        var construtora = contexto.ConstrutoraQueGrava;
        var agora = relogio.GetUtcNow();

        var reservadas = await contexto.Database.ExecuteSqlAsync(
            $"""
            insert into crm.chaves_de_idempotencia (construtora_id, chave, impressao_do_pedido, criada_em)
            values ({construtora}, {chave}, {impressaoDoPedido}, {agora})
            on conflict (construtora_id, chave) do nothing
            """,
            cancelamento);

        if (reservadas == 1)
        {
            return null;
        }

        var gravada = await contexto.ChavesDeIdempotencia
            .AsNoTracking()
            .SingleAsync(registro => registro.Chave == chave, cancelamento);

        if (gravada.ImpressaoDoPedido != impressaoDoPedido)
        {
            throw new ChaveDeIdempotenciaReutilizada(
                $"A chave de idempotência {chave} já foi usada num pedido diferente deste. Pedido novo leva chave nova.");
        }

        return gravada.ParaResposta();
    }

    /// <summary>Guarda a resposta na chave reservada por <see cref="Reservar"/>, na mesma transação.</summary>
    internal async Task Guardar(string chave, RespostaGuardada resposta, CancellationToken cancelamento)
    {
        var cabecalhos = RegistroDeIdempotencia.EscreverCabecalhos(resposta.Cabecalhos);

        var guardadas = await contexto.ChavesDeIdempotencia
            .Where(registro => registro.Chave == chave)
            .ExecuteUpdateAsync(
                colunas => colunas
                    .SetProperty(registro => registro.Status, (short)resposta.Status)
                    .SetProperty(registro => registro.Cabecalhos, cabecalhos)
                    .SetProperty(registro => registro.Corpo, resposta.Corpo),
                cancelamento);

        if (guardadas != 1)
        {
            throw new InvalidOperationException($"A chave de idempotência {chave} não está reservada: reserve antes de guardar a resposta.");
        }
    }

    private static void Validar(string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);

        if (chave.Length > TamanhoMaximoDaChave)
        {
            throw new ArgumentException($"A chave de idempotência tem mais de {TamanhoMaximoDaChave} caracteres.", nameof(chave));
        }
    }
}
