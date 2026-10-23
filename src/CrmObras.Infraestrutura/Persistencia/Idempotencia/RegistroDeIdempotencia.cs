using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmObras.Infraestrutura.Persistencia.Idempotencia;

/// <summary>
/// Uma chave de idempotência gravada: o pedido que ela atendeu, pela
/// impressão dele, e a resposta dada.
/// </summary>
/// <remarks>
/// A linha nasce na reserva, sem resposta, e recebe a resposta antes do fim
/// da mesma transação. Fora dela, ninguém vê uma chave reservada sem
/// resposta: ou vê a chave completa, ou não vê chave nenhuma.
/// </remarks>
internal sealed class RegistroDeIdempotencia
{
    public Guid ConstrutoraId { get; set; }

    public string Chave { get; set; } = string.Empty;

    /// <summary>O que identifica o pedido — método, caminho e corpo —, para reconhecer o mesmo pedido de novo.</summary>
    public string ImpressaoDoPedido { get; set; } = string.Empty;

    public short? Status { get; set; }

    /// <summary>Os cabeçalhos da resposta, num objeto JSON — coluna <c>jsonb</c>.</summary>
    public string? Cabecalhos { get; set; }

    public string? Corpo { get; set; }

    public DateTimeOffset CriadaEm { get; set; }

    public static string EscreverCabecalhos(IReadOnlyDictionary<string, string> cabecalhos) =>
        JsonSerializer.Serialize(cabecalhos.ToDictionary(), CabecalhosEmJson.Default.DictionaryStringString);

    public RespostaGuardada ParaResposta()
    {
        if (Status is null)
        {
            throw new InvalidOperationException($"A chave de idempotência {Chave} foi gravada sem a resposta.");
        }

        var cabecalhos = Cabecalhos is null
            ? []
            : JsonSerializer.Deserialize(Cabecalhos, CabecalhosEmJson.Default.DictionaryStringString) ?? [];

        return new RespostaGuardada(Status.Value, cabecalhos, Corpo ?? string.Empty);
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CabecalhosEmJson : JsonSerializerContext;
