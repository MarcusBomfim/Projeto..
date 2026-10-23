namespace CrmObras.Infraestrutura.Persistencia.Idempotencia;

/// <summary>
/// A resposta dada a um pedido com chave de idempotência: o que se devolve,
/// igual, quando o mesmo pedido chega de novo (ADR-0013).
/// </summary>
/// <param name="Status">O código HTTP.</param>
/// <param name="Cabecalhos">Os cabeçalhos que fazem parte do resultado — <c>Location</c>, <c>ETag</c>.</param>
/// <param name="Corpo">O corpo, como foi enviado; vazio se não houve.</param>
public sealed record RespostaGuardada(int Status, IReadOnlyDictionary<string, string> Cabecalhos, string Corpo);
