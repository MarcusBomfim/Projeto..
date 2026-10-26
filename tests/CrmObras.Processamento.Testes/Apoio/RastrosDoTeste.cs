using System.Diagnostics;
using CrmObras.Api.Testes;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>
/// Os rastros como o OpenTelemetry os exportaria: o SDK de verdade — com a
/// amostragem e a supressão dele —, só que exportando para a memória. As
/// fontes são as do sistema, a do Npgsql e a do cliente HTTP, como no host.
/// </summary>
/// <remarks>
/// O SDK escuta o processo inteiro. Os testes deste assembly rodam um de
/// cada vez, então os rastros são só do teste que está rodando.
/// </remarks>
public sealed class RastrosDoTeste : IDisposable
{
    /// <summary>A fonte do "pedido" que o teste simula: na API, seria o pedido HTTP.</summary>
    public static readonly ActivitySource Fonte = new("CrmObras.Testes");

    private readonly RastrosExportados _exportados = [];

    private readonly TracerProvider _provedor;

    public RastrosDoTeste() =>
        _provedor = Sdk.CreateTracerProviderBuilder()
            .AddSource("CrmObras.*", "Npgsql", "System.Net.Http")
            .AddInMemoryExporter(_exportados)
            .Build();

    /// <summary>Tudo o que terminou até agora.</summary>
    public IReadOnlyList<Activity> Todos
    {
        get
        {
            _provedor.ForceFlush();

            return [.. _exportados];
        }
    }

    public IReadOnlyList<Activity> Do(ActivityTraceId rastro) => _exportados.Do(rastro);

    public void Dispose() => _provedor.Dispose();
}
