using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Processamento.Rotinas;

/// <summary>
/// Uma rotina de manutenção, rodada de tempos em tempos.
/// </summary>
/// <remarks>
/// Todo processador roda todas as rotinas: com dois processadores no ar, a
/// mesma rotina roda nos dois. Por isso cada uma tem de poder rodar duas vezes
/// ao mesmo tempo sem estragar nada — apagar o que já foi apagado não apaga
/// mais nada.
/// </remarks>
public interface IRotina
{
    TimeSpan Intervalo { get; }

    Task Executar(IServiceProvider servicos, CancellationToken cancelamento);
}

/// <summary>
/// Apaga as chaves de idempotência com mais de 24 horas. Quem repete um pedido
/// depois disso não está repetindo — está pedindo de novo (ADR-0013).
/// </summary>
public sealed partial class LimpezaDasChavesDeIdempotencia(TimeProvider relogio, ILogger<LimpezaDasChavesDeIdempotencia> registro) : IRotina
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    public TimeSpan Intervalo => TimeSpan.FromHours(1);

    public async Task Executar(IServiceProvider servicos, CancellationToken cancelamento)
    {
        var apagadas = await servicos.GetRequiredService<ManutencaoDoBanco>()
            .ApagarChavesDeIdempotenciaAntesDe(relogio.GetUtcNow() - Validade, cancelamento);

        Apagou(registro, apagadas);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Apagadas} chave(s) de idempotência vencida(s) apagada(s).")]
    private static partial void Apagou(ILogger registro, int apagadas);
}

/// <summary>
/// Apaga as mensagens tratadas há mais de 7 dias — tempo de sobra para
/// investigar um problema com elas. A abandonada fica: alguém precisa ver por
/// que ela não foi tratada (ADR-0016).
/// </summary>
public sealed partial class LimpezaDaCaixaDeSaida(TimeProvider relogio, ILogger<LimpezaDaCaixaDeSaida> registro) : IRotina
{
    public static readonly TimeSpan Guarda = TimeSpan.FromDays(7);

    public TimeSpan Intervalo => TimeSpan.FromHours(1);

    public async Task Executar(IServiceProvider servicos, CancellationToken cancelamento)
    {
        var apagadas = await servicos.GetRequiredService<ManutencaoDoBanco>()
            .ApagarMensagensTratadasAntesDe(relogio.GetUtcNow() - Guarda, cancelamento);

        Apagou(registro, apagadas);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Apagadas} mensagem(ns) tratada(s) apagada(s) da caixa de saída.")]
    private static partial void Apagou(ILogger registro, int apagadas);
}
