using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Api.Testes;

/// <summary>
/// Um coletor OpenTelemetry de mentira, numa porta livre: recebe por OTLP
/// sobre HTTP e anota o caminho de cada envio — <c>/v1/traces</c>,
/// <c>/v1/metrics</c>, <c>/v1/logs</c>. O conteúdo não interessa aqui; que
/// ele chegou, sim.
/// </summary>
public sealed class ColetorDeTeste : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ColetorDeTeste(WebApplication app, string endereco, ConcurrentQueue<string> recebidos)
    {
        _app = app;
        Endereco = endereco;
        Recebidos = recebidos;
    }

    public string Endereco { get; }

    public ConcurrentQueue<string> Recebidos { get; }

    public static async Task<ColetorDeTeste> Subir()
    {
        var recebidos = new ConcurrentQueue<string>();
        var construtor = WebApplication.CreateSlimBuilder();
        construtor.WebHost.UseUrls("http://127.0.0.1:0");

        var app = construtor.Build();
        app.MapPost("/v1/{sinal}", (HttpRequest pedido) =>
        {
            recebidos.Enqueue(pedido.Path.Value ?? string.Empty);

            return Results.Ok();
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        var endereco = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.Single();

        return new ColetorDeTeste(app, endereco, recebidos);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
