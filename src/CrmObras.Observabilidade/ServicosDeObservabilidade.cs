using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CrmObras.Observabilidade;

/// <summary>Rastros, métricas e logs pelo OpenTelemetry, iguais nos dois serviços do sistema (ADR-0018).</summary>
public static class ServicosDeObservabilidade
{
    /// <summary>
    /// O começo do nome das fontes de rastro e dos medidores do sistema — o
    /// que começa assim é coletado, sem precisar registrar cada um.
    /// </summary>
    public const string Prefixo = "CrmObras.";

    /// <summary>A fonte e o medidor que o próprio <see cref="System.Net.Http.HttpClient"/> tem, desde o .NET 9: sem pacote de instrumentação.</summary>
    private const string ClienteHttp = "System.Net.Http";

    /// <summary>
    /// Instrumenta o serviço: os pedidos HTTP que ele atende e os que ele faz
    /// — o processador, ao planejamento-obras, levando o rastro adiante —, os
    /// comandos no PostgreSQL, o runtime do .NET e as fontes do próprio
    /// sistema, com os logs no mesmo rastro.
    /// </summary>
    /// <remarks>
    /// Para onde a telemetria vai é configuração do ambiente, pelas variáveis
    /// padrão do OpenTelemetry — <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> e as
    /// outras —, e não do código: o mesmo serviço manda para o painel local,
    /// para um coletor ou para um fornecedor. Sem ela, nada sai do processo;
    /// o rastro continua existindo, e é ele que dá o <c>traceId</c> dos
    /// erros.
    /// </remarks>
    /// <param name="servico">O nome do serviço nos rastros: <c>crm-obras-api</c>.</param>
    public static TBuilder AdicionarObservabilidade<TBuilder>(this TBuilder builder, string servico)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(servico);

        var telemetria = builder.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso
                .AddService(servico, serviceNamespace: "crm-obras", serviceVersion: Versao())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(rastros => rastros
                .AddSource($"{Prefixo}*", ClienteHttp)
                .AddAspNetCoreInstrumentation(opcoes => opcoes.Filter = contexto => !Saude.EhVerificacao(contexto.Request.Path))
                .AddNpgsql())
            .WithMetrics(metricas => metricas
                .AddMeter($"{Prefixo}*")
                .AddAspNetCoreInstrumentation()
                .AddMeter("System.Runtime", "Npgsql", ClienteHttp))
            .WithLogging(configureBuilder: null, configureOptions: logs =>
            {
                logs.IncludeFormattedMessage = true;
                logs.IncludeScopes = true;
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetria.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>A versão do serviço, com o commit de que foi compilado quando o build sabe qual: <c>0.1.0+3fa2c1d…</c>.</summary>
    private static string? Versao() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
