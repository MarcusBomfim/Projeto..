namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// A construtora de uma tarefa em segundo plano. Começa nenhuma — a tarefa
/// ainda não sabe para quem vai trabalhar —, e passa a ser a da mensagem que
/// ela pegou da fila (ADR-0016).
/// </summary>
/// <remarks>
/// Uma por escopo, e uma construtora por escopo: assumir outra no meio do
/// caminho é engano, e é recusado.
/// </remarks>
public sealed class ConstrutoraAssumida : IConstrutoraAtual
{
    public Guid? Id { get; private set; }

    public void Assumir(Guid construtora)
    {
        if (construtora == Guid.Empty)
        {
            throw new ArgumentException("A construtora não pode ser o Guid vazio.", nameof(construtora));
        }

        if (Id is { } atual && atual != construtora)
        {
            throw new InvalidOperationException($"Esta tarefa já trabalha em nome da construtora {atual}; uma tarefa, uma construtora.");
        }

        Id = construtora;
    }
}
