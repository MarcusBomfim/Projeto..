namespace CrmObras.Infraestrutura.Persistencia.Idempotencia;

/// <summary>O que um pedido com chave de idempotência recebeu.</summary>
/// <param name="Resposta">A resposta: a da operação, ou a guardada da primeira vez.</param>
/// <param name="Repetido">Se o pedido já tinha sido atendido — e a operação, desta vez, não rodou.</param>
public sealed record PedidoAtendido(RespostaGuardada Resposta, bool Repetido);
