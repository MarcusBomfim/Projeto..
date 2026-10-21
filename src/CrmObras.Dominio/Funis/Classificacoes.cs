using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Funis;

/// <summary>A identidade de um funil.</summary>
public sealed record FunilId : Identificador
{
    public FunilId(Guid valor)
        : base(valor)
    {
    }

    public static FunilId Novo() => new(Guid.CreateVersion7());
}

/// <summary>A identidade de uma etapa, dentro do funil dela.</summary>
public sealed record EtapaId : Identificador
{
    public EtapaId(Guid valor)
        : base(valor)
    {
    }

    public static EtapaId Novo() => new(Guid.CreateVersion7());
}

/// <summary>Se a venda ainda está andando na etapa, ou terminou nela.</summary>
public enum TipoDeEtapa
{
    /// <summary>A venda está em andamento: primeiro contato, visita, proposta.</summary>
    Aberta,

    /// <summary>A obra foi vendida.</summary>
    Ganha,

    /// <summary>A obra não foi vendida, e o motivo ficou registrado.</summary>
    Perdida,
}

/// <summary>
/// O que a oportunidade precisa ter para entrar numa etapa. Combinam-se:
/// <c>ValorEstimado | PrevisaoDeFechamento</c>.
/// </summary>
[Flags]
public enum Requisitos
{
    Nenhum = 0,

    /// <summary>Quanto a obra deve valer.</summary>
    ValorEstimado = 1,

    /// <summary>Quando a venda deve fechar.</summary>
    PrevisaoDeFechamento = 2,

    /// <summary>Quem decide a compra, entre os contatos do cliente.</summary>
    QuemDecide = 4,
}
