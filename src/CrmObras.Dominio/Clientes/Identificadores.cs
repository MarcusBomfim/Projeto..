using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Clientes;

/// <summary>A identidade de uma conta.</summary>
public sealed record ContaId : Identificador
{
    public ContaId(Guid valor)
        : base(valor)
    {
    }

    public static ContaId Novo() => new(Guid.CreateVersion7());
}

/// <summary>A identidade de um contato, dentro da conta dele.</summary>
public sealed record ContatoId : Identificador
{
    public ContatoId(Guid valor)
        : base(valor)
    {
    }

    public static ContatoId Novo() => new(Guid.CreateVersion7());
}
