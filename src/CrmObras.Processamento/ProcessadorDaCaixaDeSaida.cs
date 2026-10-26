using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using CrmObras.Processamento.Hospedagem;

namespace CrmObras.Processamento;

/// <summary>
/// Trata a caixa de saída, uma mensagem por transação: reserva a próxima,
/// entrega aos tratadores do tipo dela, em nome da construtora dela, e marca a
/// mensagem como tratada — tudo na mesma transação (ADR-0017).
/// </summary>
/// <remarks>
/// <para>
/// O que o tratador grava e a marca de tratada são confirmados juntos: ou a
/// mensagem foi tratada e o efeito ficou, ou nenhum dos dois.
/// </para>
/// <para>
/// Se um tratador falha, o que ele gravou é desfeito até o ponto anterior ao
/// tratamento — a mensagem continua reservada —, a falha é registrada nela, e
/// ela volta depois de uma espera que dobra a cada falha. Tentativas demais,
/// e a fila desiste dela.
/// </para>
/// <para>
/// O tratamento continua o rastro de quem publicou a mensagem, e cada
/// tentativa é medida (ADR-0018). Olhar a fila, não: a reserva roda sem
/// rastro, porque a volta vazia acontece a cada aviso e a cada intervalo, e
/// encheria o painel de rastros sem história.
/// </para>
/// </remarks>
public sealed partial class ProcessadorDaCaixaDeSaida(
    IServiceScopeFactory escopos,
    MetricasDaCaixaDeSaida metricas,
    PulsoDaFila pulso,
    TimeProvider relogio,
    ILogger<ProcessadorDaCaixaDeSaida> registro)
{
    private const string AntesDoTratamento = "antes_do_tratamento";

    /// <summary>Trata as mensagens pendentes até não sobrar nenhuma pronta.</summary>
    /// <returns>Quantas foram tratadas — com sucesso ou não.</returns>
    public async Task<int> ProcessarPendentes(CancellationToken cancelamento = default)
    {
        var tratadas = 0;

        while (await ProcessarUma(cancelamento))
        {
            tratadas++;
        }

        return tratadas;
    }

    /// <returns>Se havia uma mensagem pronta para tratar.</returns>
    public async Task<bool> ProcessarUma(CancellationToken cancelamento = default)
    {
        pulso.Bater();

        await using var escopo = escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;
        var contexto = servicos.GetRequiredService<ContextoDoBanco>();
        var fila = servicos.GetRequiredService<FilaDaCaixaDeSaida>();

        await using var transacao = await SemRastro(() => contexto.Database.BeginTransactionAsync(cancelamento));

        if (await SemRastro(() => fila.ReservarProxima(cancelamento)) is not { } mensagem)
        {
            return false;
        }

        var inicio = relogio.GetTimestamp();
        using var tratamento = Tratamento(mensagem);
        string resultado;

        await transacao.CreateSavepointAsync(AntesDoTratamento, cancelamento);

        try
        {
            await Despachante.Despachar(servicos, mensagem.Ler(), cancelamento);
            await fila.MarcarTratada(mensagem.Id, cancelamento);
            Tratada(registro, mensagem.Id, mensagem.Tipo);
            resultado = "tratada";
        }
        catch (Exception erro) when (!cancelamento.IsCancellationRequested)
        {
            // Um tratador falhar não pode parar a fila — nem um tempo esgotado
            // lá dentro, que também chega como cancelamento: só o host parando
            // interrompe o tratamento. O que o tratador gravou sai, e a falha
            // fica registrada na mensagem, que volta mais tarde.
            await transacao.RollbackToSavepointAsync(AntesDoTratamento, cancelamento);
            contexto.ChangeTracker.Clear();

            tratamento?.SetStatus(ActivityStatusCode.Error, erro.Message);
            tratamento?.AddException(erro);

            if (await fila.RegistrarFalha(mensagem, erro, cancelamento))
            {
                Abandonada(registro, erro, mensagem.Id, mensagem.Tipo, mensagem.Tentativas + 1);
                resultado = "abandonada";
            }
            else
            {
                Falhou(registro, erro, mensagem.Id, mensagem.Tipo, mensagem.Tentativas + 1);
                resultado = "falhou";
            }
        }

        await transacao.CommitAsync(cancelamento);

        tratamento?.SetTag("crm.mensagem.resultado", resultado);
        metricas.Registrar(mensagem, resultado, relogio.GetUtcNow() - mensagem.OcorridaEm, relogio.GetElapsedTime(inicio));

        return true;
    }

    /// <summary>
    /// O tratamento, no rastro de quem publicou a mensagem. Sem o contexto —
    /// mensagem gravada sem ninguém rastreando —, começa um rastro novo.
    /// </summary>
    private static Activity? Tratamento(MensagemReservada mensagem)
    {
        ActivityContext.TryParse(mensagem.ContextoDoRastreio, traceState: null, isRemote: true, out var publicacao);

        var atributos = TelemetriaDaCaixaDeSaida.Atributos(mensagem.Id, mensagem.Tipo, mensagem.Construtora, "process");
        atributos.Add("crm.mensagem.tentativa", mensagem.Tentativas + 1);

        return TelemetriaDoProcessamento.Fonte.StartActivity($"tratar {mensagem.Tipo}", ActivityKind.Consumer, publicacao, atributos);
    }

    private static async Task<T> SemRastro<T>(Func<Task<T>> operacao)
    {
        using var silencio = SuppressInstrumentationScope.Begin();

        return await operacao();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "A mensagem {Mensagem} ({Tipo}) foi tratada.")]
    private static partial void Tratada(ILogger registro, Guid mensagem, string tipo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A mensagem {Mensagem} ({Tipo}) falhou na tentativa {Tentativa}.")]
    private static partial void Falhou(ILogger registro, Exception erro, Guid mensagem, string tipo, int tentativa);

    [LoggerMessage(Level = LogLevel.Error, Message = "A mensagem {Mensagem} ({Tipo}) falhou {Tentativas} vezes e foi abandonada: alguém precisa ver por quê.")]
    private static partial void Abandonada(ILogger registro, Exception erro, Guid mensagem, string tipo, int tentativas);
}
