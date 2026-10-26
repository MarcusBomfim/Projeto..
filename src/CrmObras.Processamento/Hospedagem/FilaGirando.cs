using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CrmObras.Processamento.Hospedagem;

/// <summary>
/// Quando a fila deu sinal de vida pela última vez. Bate a cada mensagem que
/// o processador vai buscar — inclusive a busca que volta vazia, e a que
/// falha com o banco fora do ar: a volta continua girando.
/// </summary>
/// <remarks>Nasce batendo: o host que acabou de subir está vivo.</remarks>
public sealed class PulsoDaFila(TimeProvider relogio)
{
    private long _ultimo = relogio.GetUtcNow().UtcTicks;

    public DateTimeOffset Ultimo => new(Interlocked.Read(ref _ultimo), TimeSpan.Zero);

    public void Bater() => Interlocked.Exchange(ref _ultimo, relogio.GetUtcNow().UtcTicks);
}

/// <summary>
/// A fila está girando: deu sinal de vida há menos de quatro intervalos. Se
/// não, o processador travou — num tratador que não volta, por exemplo —, e
/// reiniciar resolve (ADR-0018).
/// </summary>
/// <remarks>
/// O banco fora do ar não para o pulso: a volta continua girando, tentando
/// de novo a cada intervalo. Quem diz que o processador não está pronto, aí,
/// é a verificação do banco — e reiniciar não resolveria.
/// </remarks>
public sealed class FilaGirando(PulsoDaFila pulso, TimeProvider relogio, IOptions<OpcoesDoProcessamento> opcoes) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var parada = relogio.GetUtcNow() - pulso.Ultimo;

        return Task.FromResult(parada <= opcoes.Value.IntervaloDaFila * 4
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"A fila não dá sinal de vida há {parada.TotalSeconds:0} segundos."));
    }
}
