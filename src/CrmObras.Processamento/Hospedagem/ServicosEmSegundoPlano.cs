using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CrmObras.Processamento.Rotinas;

namespace CrmObras.Processamento.Hospedagem;

/// <summary>O que o processamento deixa configurar.</summary>
public sealed class OpcoesDoProcessamento
{
    /// <summary>
    /// De quanto em quanto tempo a fila é olhada mesmo sem aviso de mensagem
    /// nova — para a mensagem que voltou de uma falha, e para quando o aviso
    /// não chega.
    /// </summary>
    public TimeSpan IntervaloDaFila { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// A volta do processador: trata o que está pendente, e espera — o aviso de
/// mensagem nova ou o intervalo —, até o host parar.
/// </summary>
internal sealed partial class ServicoDaCaixaDeSaida(
    ProcessadorDaCaixaDeSaida processador,
    EsperaPorMensagens espera,
    IOptions<OpcoesDoProcessamento> opcoes,
    ILogger<ServicoDaCaixaDeSaida> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await espera.Preparar(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processador.ProcessarPendentes(stoppingToken);
            }
            catch (Exception erro) when (!stoppingToken.IsCancellationRequested)
            {
                // O banco fora do ar não derruba o processador: ele tenta de
                // novo na volta seguinte.
                VoltaFalhou(registro, erro);
            }

            Esperando(registro, opcoes.Value.IntervaloDaFila);
            await espera.Aguardar(opcoes.Value.IntervaloDaFila, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A volta do processador da caixa de saída falhou.")]
    private static partial void VoltaFalhou(ILogger registro, Exception erro);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fila em dia; esperando mensagem nova por até {Intervalo}.")]
    private static partial void Esperando(ILogger registro, TimeSpan intervalo);
}

/// <summary>Roda cada rotina de manutenção no intervalo dela, cada vez num escopo novo.</summary>
internal sealed partial class ServicoDasRotinas(
    IEnumerable<IRotina> rotinas,
    IServiceScopeFactory escopos,
    TimeProvider relogio,
    ILogger<ServicoDasRotinas> registro) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(rotinas.Select(rotina => Rodar(rotina, stoppingToken)));

    private async Task Rodar(IRotina rotina, CancellationToken cancelamento)
    {
        using var proxima = new PeriodicTimer(rotina.Intervalo, relogio);

        do
        {
            // Cada execução, um rastro: os comandos dela no banco ficam dentro dele.
            using var execucao = TelemetriaDoProcessamento.Fonte.StartActivity($"rotina {rotina.GetType().Name}");

            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await rotina.Executar(escopo.ServiceProvider, cancelamento);
            }
            catch (Exception erro) when (!cancelamento.IsCancellationRequested)
            {
                execucao?.SetStatus(ActivityStatusCode.Error, erro.Message);
                execucao?.AddException(erro);
                RotinaFalhou(registro, erro, rotina.GetType().Name);
            }
        }
        while (await proxima.WaitForNextTickAsync(cancelamento));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A rotina {Rotina} falhou; roda de novo no próximo intervalo.")]
    private static partial void RotinaFalhou(ILogger registro, Exception erro, string rotina);
}
