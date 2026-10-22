using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// A serialização dos documentos, gerada em tempo de compilação.
/// </summary>
/// <remarks>
/// <para>
/// Geração de código em vez de reflexão: o compilador vê todos os tipos que
/// vão para o banco, e um tipo que o serializador não sabe tratar é erro de
/// build.
/// </para>
/// <para>
/// A leitura é estrita de propósito (ADR-0011): propriedade obrigatória que
/// falta, nulo onde não cabe nulo ou propriedade que o documento não conhece
/// fazem a leitura falhar. Um documento que não é exatamente o formato
/// esperado não vira, em silêncio, um agregado com um campo zerado.
/// </para>
/// <para>
/// Enums vão como texto — <c>"ConcorrenteEscolhido"</c>, e não <c>2</c> —:
/// legíveis numa consulta, e imunes a uma reordenação dos membros. E só como
/// texto: o número, que o conversor padrão aceitaria, deixaria entrar um
/// valor que o enum não tem. Os requisitos de uma etapa, que se combinam, vão
/// como a lista que o .NET escreve: <c>"ValorEstimado, PrevisaoDeFechamento"</c>.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    Converters =
    [
        typeof(SoTexto<PapelNaCompra>),
        typeof(SoTexto<OrigemDoLead>),
        typeof(SoTexto<SituacaoDoLead>),
        typeof(SoTexto<Resposta>),
        typeof(SoTexto<MotivoDoDescarte>),
        typeof(SoTexto<Requisitos>),
        typeof(SoTexto<SituacaoDaOportunidade>),
        typeof(SoTexto<MotivoDaPerda>),
        typeof(SoTexto<TipoDeAtividade>),
        typeof(SoTexto<SituacaoDaAtividade>),
        typeof(SoTexto<SituacaoDaVersao>),
        typeof(SoTexto<Cargo>),
    ])]
[JsonSerializable(typeof(DocumentoDaConta))]
[JsonSerializable(typeof(DocumentoDoLead))]
[JsonSerializable(typeof(DocumentoDoFunil))]
[JsonSerializable(typeof(DocumentoDaOportunidade))]
[JsonSerializable(typeof(DocumentoDaAtividade))]
[JsonSerializable(typeof(DocumentoDaProposta))]
internal sealed partial class ContextoJson : JsonSerializerContext;

/// <summary>Um enum gravado pelo nome, e lido só pelo nome — nunca pelo número.</summary>
internal sealed class SoTexto<T>() : JsonStringEnumConverter<T>(namingPolicy: null, allowIntegerValues: false)
    where T : struct, Enum;

/// <summary>Escreve e lê documentos pelo <see cref="ContextoJson"/>.</summary>
internal static class Json
{
    public static string Escrever<T>(T documento, JsonTypeInfo<T> tipo) => JsonSerializer.Serialize(documento, tipo);

    public static T Ler<T>(string json, JsonTypeInfo<T> tipo) =>
        JsonSerializer.Deserialize(json, tipo) ?? throw new JsonException("O documento gravado é nulo.");
}
