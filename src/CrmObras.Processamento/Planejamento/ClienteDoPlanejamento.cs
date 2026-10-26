using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmObras.Processamento.Planejamento;

/// <summary>
/// A API do planejamento-obras, do jeito que o CRM a usa: abrir o cronograma
/// de uma obra vendida (ADR-0017).
/// </summary>
/// <remarks>
/// O pedido segue o contrato do planejamento: o token de serviço, e a chave
/// de idempotência que torna a repetição inofensiva — a mesma chave devolve o
/// mesmo cronograma, em vez de abrir outro. O rastro do tratamento vai junto,
/// no <c>traceparent</c>, pelo próprio <see cref="HttpClient"/>.
/// </remarks>
public sealed class ClienteDoPlanejamento(HttpClient http, TokenDeServico tokens)
{
    public const string ChaveDeIdempotencia = "Idempotency-Key";

    /// <summary>O cabeçalho com que o planejamento diz que a resposta é a guardada de um pedido igual.</summary>
    public const string Repetida = "Idempotent-Replayed";

    /// <summary>Abre o cronograma da obra, em nome da construtora.</summary>
    /// <param name="chave">A chave de idempotência: a mesma em cada nova tentativa do mesmo pedido.</param>
    /// <exception cref="FalhaNoPlanejamento">O planejamento não abriu o cronograma, com o que ele disse.</exception>
    public async Task<CronogramaAberto> AbrirCronograma(Guid construtora, string obra, string chave, CancellationToken cancelamento = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(obra);
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);

        using var pedido = new HttpRequestMessage(HttpMethod.Post, "api/cronogramas")
        {
            Content = JsonContent.Create(new NovoCronograma(obra), ContextoJsonDoPlanejamento.Default.NovoCronograma),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Para(construtora));
        pedido.Headers.Add(ChaveDeIdempotencia, chave);

        using var resposta = await http.SendAsync(pedido, cancelamento);

        if (resposta.StatusCode != HttpStatusCode.Created)
        {
            throw new FalhaNoPlanejamento(
                $"O planejamento-obras respondeu {(int)resposta.StatusCode} ao abrir o cronograma: {await Explicacao(resposta, cancelamento)}",
                resposta.StatusCode);
        }

        var criado = await resposta.Content.ReadFromJsonAsync(ContextoJsonDoPlanejamento.Default.CronogramaCriado, cancelamento)
            ?? throw new FalhaNoPlanejamento("O planejamento-obras abriu o cronograma, mas a resposta veio vazia.", resposta.StatusCode);

        return new CronogramaAberto(criado.Id, resposta.Headers.Contains(Repetida));
    }

    /// <summary>O que o planejamento disse: o detalhe do ProblemDetails, ou o título, ou o começo do corpo.</summary>
    private static async Task<string> Explicacao(HttpResponseMessage resposta, CancellationToken cancelamento)
    {
        var corpo = await resposta.Content.ReadAsStringAsync(cancelamento);

        try
        {
            var problema = JsonSerializer.Deserialize(corpo, ContextoJsonDoPlanejamento.Default.Problema);
            var explicacao = string.IsNullOrWhiteSpace(problema?.Detail) ? problema?.Title : problema.Detail;

            if (!string.IsNullOrWhiteSpace(explicacao))
            {
                return explicacao;
            }
        }
        catch (JsonException)
        {
            // Não é ProblemDetails: um proxy na frente, uma página de erro.
        }

        return string.IsNullOrWhiteSpace(corpo)
            ? resposta.ReasonPhrase ?? "sem explicação."
            : corpo[..Math.Min(corpo.Length, 200)];
    }
}

/// <summary>O cronograma aberto no planejamento.</summary>
/// <param name="Repetido">Se o planejamento já o tinha aberto, por um pedido igual — uma tentativa anterior que ficou sem confirmação.</param>
public sealed record CronogramaAberto(Guid Id, bool Repetido);

/// <summary>O planejamento-obras não fez o que se pediu, ou não respondeu como o contrato diz.</summary>
public sealed class FalhaNoPlanejamento : Exception
{
    public FalhaNoPlanejamento()
    {
    }

    public FalhaNoPlanejamento(string message)
        : base(message)
    {
    }

    public FalhaNoPlanejamento(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public FalhaNoPlanejamento(string message, HttpStatusCode status)
        : base(message) => Status = status;

    /// <summary>O que o planejamento respondeu.</summary>
    public HttpStatusCode? Status { get; }
}

internal sealed record NovoCronograma(string Nome);

/// <summary>Do cronograma criado, o CRM só precisa do id; o resto da resposta é do planejamento.</summary>
internal sealed record CronogramaCriado(Guid Id);

internal sealed record ProblemaDoPlanejamento(string? Title, string? Detail);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(NovoCronograma))]
[JsonSerializable(typeof(CronogramaCriado))]
[JsonSerializable(typeof(ProblemaDoPlanejamento), TypeInfoPropertyName = "Problema")]
internal sealed partial class ContextoJsonDoPlanejamento : JsonSerializerContext;
