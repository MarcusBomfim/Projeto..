using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>Um pedido que chegou ao planejamento de mentira, com o que o teste confere.</summary>
public sealed record PedidoRecebido(string Autorizacao, string ChaveDeIdempotencia, string Traceparent, string Corpo)
{
    /// <summary>O nome do cronograma pedido.</summary>
    public string? Nome => JsonDocument.Parse(Corpo).RootElement.GetProperty("nome").GetString();
}

/// <summary>
/// O planejamento-obras de mentira: um servidor HTTP de verdade, numa porta
/// qualquer da máquina, que atende o <c>POST /api/cronogramas</c> como o de
/// verdade — com a chave de idempotência: a mesma chave, o mesmo cronograma.
/// </summary>
/// <remarks>
/// Servidor de verdade, e não um <see cref="HttpMessageHandler"/> no lugar do
/// cliente: é o <see cref="HttpClient"/> inteiro que vai ao planejamento, com
/// o tempo-limite, o <c>traceparent</c> e o resto que só ele põe no pedido.
/// </remarks>
public sealed class PlanejamentoDeMentira : IAsyncDisposable
{
    private readonly WebApplication _app;

    private readonly ConcurrentQueue<PedidoRecebido> _recebidos = new();

    private readonly ConcurrentDictionary<string, Guid> _abertos = new();

    private readonly ConcurrentQueue<Func<HttpContext, Task<IResult>>> _proximas = new();

    private PlanejamentoDeMentira(WebApplication app)
    {
        _app = app;
        _app.MapPost("/api/cronogramas", (Delegate)AbrirCronograma);
    }

    public Uri Endereco { get; private set; } = new("http://127.0.0.1/");

    public IReadOnlyList<PedidoRecebido> Recebidos => [.. _recebidos];

    /// <summary>Os cronogramas abertos, pela chave de idempotência com que foram pedidos.</summary>
    public IReadOnlyDictionary<string, Guid> Abertos => _abertos;

    public static async Task<PlanejamentoDeMentira> Subir()
    {
        var construtor = WebApplication.CreateSlimBuilder();
        construtor.WebHost.UseUrls("http://127.0.0.1:0");
        construtor.Logging.ClearProviders();

        var mentira = new PlanejamentoDeMentira(construtor.Build());
        await mentira._app.StartAsync();

        var endereco = mentira._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        mentira.Endereco = new Uri($"{endereco}/");

        return mentira;
    }

    /// <summary>A próxima resposta é esta falha; depois dela, o planejamento volta a abrir cronogramas.</summary>
    public void FalharUmaVez(HttpStatusCode status, string corpo = "", string tipo = "application/problem+json") =>
        _proximas.Enqueue(_ => Task.FromResult(Results.Text(corpo, tipo, statusCode: (int)status)));

    /// <summary>A próxima resposta demora mais do que o tempo dado.</summary>
    public void DemorarUmaVez(TimeSpan quanto) =>
        _proximas.Enqueue(async http =>
        {
            await Task.Delay(quanto, http.RequestAborted);

            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
        });

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private async Task<IResult> AbrirCronograma(HttpContext http)
    {
        using var leitor = new StreamReader(http.Request.Body);
        var corpo = await leitor.ReadToEndAsync(http.RequestAborted);
        var chave = http.Request.Headers["Idempotency-Key"].ToString();

        _recebidos.Enqueue(new PedidoRecebido(
            http.Request.Headers.Authorization.ToString(),
            chave,
            http.Request.Headers["traceparent"].ToString(),
            corpo));

        if (_proximas.TryDequeue(out var proxima))
        {
            return await proxima(http);
        }

        if (_abertos.ContainsKey(chave))
        {
            http.Response.Headers["Idempotent-Replayed"] = "true";
        }

        var id = _abertos.GetOrAdd(chave, _ => Guid.CreateVersion7());
        var nome = JsonDocument.Parse(corpo).RootElement.GetProperty("nome").GetString();

        return Results.Created($"/api/cronogramas/{id}", new { id, nome, atividades = Array.Empty<object>(), dependencias = Array.Empty<object>() });
    }
}
