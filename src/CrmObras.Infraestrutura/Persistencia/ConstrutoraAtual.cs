namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// A construtora em nome de quem o contexto lê e grava. Cada construtora só
/// enxerga os clientes, as vendas e as propostas dela (ADR-0011).
/// </summary>
/// <remarks>
/// Quem hospeda a persistência diz qual é: a API tira do token de quem fez o
/// pedido; os testes e as ferramentas usam uma <see cref="ConstrutoraFixa"/>.
/// Sem construtora — <see langword="null"/> —, toda consulta volta vazia e
/// nada se grava.
/// </remarks>
public interface IConstrutoraAtual
{
    Guid? Id { get; }
}

/// <summary>Uma construtora só, a mesma o tempo todo: para testes, ferramentas e tarefas em nome de uma construtora.</summary>
public sealed record ConstrutoraFixa : IConstrutoraAtual
{
    public ConstrutoraFixa(Guid? id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A construtora não pode ser o Guid vazio: para nenhuma, use ConstrutoraFixa.Nenhuma.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Nenhuma construtora: para quem não lê nem grava agregado — o dotnet-ef gerando migrações, por exemplo.</summary>
    public static ConstrutoraFixa Nenhuma { get; } = new(id: null);

    public Guid? Id { get; }
}
