using System.Diagnostics;
using CrmObras.Api.Testes;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Planejamento;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CrmObras.Processamento.Testes;

/*
 * A telemetria do host do processador como o Program.cs a monta (ADR-0018):
 * o serviço com o nome dele, e o pedido ao planejamento rastreado pelo
 * próprio HttpClient — é ele que leva o rastro ao outro sistema.
 */
public sealed class ObservabilidadeDoProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    [Fact]
    public async Task O_host_rastreia_o_pedido_ao_planejamento_com_o_nome_do_servico()
    {
        var exportados = new RastrosExportados();
        await using var host = HostDoProcessador.Montar(
            Banco.ConexaoDoProcessador,
            Planejamento.Endereco,
            Relogio,
            servicos => servicos.ConfigureOpenTelemetryTracerProvider(rastros => rastros.AddInMemoryExporter(exportados)));
        var provedor = host.Services.GetRequiredService<TracerProvider>();
        await using var escopo = host.Services.CreateAsyncScope();

        await escopo.ServiceProvider.GetRequiredService<ClienteDoPlanejamento>()
            .AbrirCronograma(Construtora, "Galpão de 1.200 m² em Jundiaí", "crm-obras:teste", Cancelamento);
        provedor.ForceFlush();

        var aoPlanejamento = Assert.Single(exportados, atividade => atividade.Source.Name == "System.Net.Http");
        Assert.Equal((ActivityKind.Client, "POST"), (aoPlanejamento.Kind, aoPlanejamento.GetTagItem("http.request.method")));
        Assert.StartsWith($"00-{aoPlanejamento.TraceId}-{aoPlanejamento.SpanId}-", Assert.Single(Planejamento.Recebidos).Traceparent, StringComparison.Ordinal);

        var servico = provedor.GetResource().Attributes.ToDictionary();
        Assert.Equal(("crm-obras-processador", "crm-obras"), (servico["service.name"], servico["service.namespace"]));
    }
}
