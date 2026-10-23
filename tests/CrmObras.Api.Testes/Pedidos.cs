using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace CrmObras.Api.Testes;

/// <summary>
/// O jeito de os testes falarem com a API: com os cabeçalhos dela —
/// <c>If-Match</c>, <c>Idempotency-Key</c> — e o JSON dela.
/// </summary>
internal static class Pedidos
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    /// <param name="corpo">Um objeto, serializado em JSON; ou uma string, enviada como está.</param>
    /// <param name="versao">A versão lida, para o <c>If-Match</c>.</param>
    /// <param name="chave">A chave de idempotência.</param>
    public static async Task<HttpResponseMessage> Enviar(
        this HttpClient cliente,
        HttpMethod metodo,
        string caminho,
        object? corpo = null,
        int? versao = null,
        string? chave = null)
    {
        using var pedido = new HttpRequestMessage(metodo, caminho);

        pedido.Content = corpo switch
        {
            null => null,
            string json => new StringContent(json, Encoding.UTF8, "application/json"),
            _ => JsonContent.Create(corpo, options: Json),
        };

        if (versao is not null)
        {
            pedido.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{versao}\""));
        }

        if (chave is not null)
        {
            pedido.Headers.Add("Idempotency-Key", chave);
        }

        return await cliente.SendAsync(pedido, Cancelamento);
    }

    /// <summary>Cria com uma chave de idempotência nova, como faz um cliente bem-comportado.</summary>
    public static Task<HttpResponseMessage> Criar(this HttpClient cliente, string caminho, object? corpo) =>
        cliente.Enviar(HttpMethod.Post, caminho, corpo, chave: Guid.NewGuid().ToString());

    public static async Task<T> Ler<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(Json, Cancelamento))!;

    public static Task<ProblemDetails> Problema(this HttpResponseMessage resposta) => resposta.Ler<ProblemDetails>();

    /// <summary>A versão que veio na ETag.</summary>
    public static int Versao(this HttpResponseMessage resposta) =>
        int.Parse(resposta.Headers.ETag!.Tag.Trim('"'), CultureInfo.InvariantCulture);
}
