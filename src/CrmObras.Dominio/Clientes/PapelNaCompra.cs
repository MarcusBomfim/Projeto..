namespace CrmObras.Dominio.Clientes;

/// <summary>Quem decide, quem influencia, quem paga e quem usa — numa obra, raramente a mesma pessoa.</summary>
public enum PapelNaCompra
{
    /// <summary>Ainda não se sabe que papel a pessoa tem na compra.</summary>
    NaoSabido,

    /// <summary>Assina o contrato: o dono, o diretor, o sócio.</summary>
    Decisor,

    /// <summary>Pesa na escolha sem assinar: o arquiteto, o engenheiro do cliente, o fiscal.</summary>
    Influenciador,

    /// <summary>Cuida do pagamento: o financeiro, o banco do financiamento.</summary>
    Financeiro,

    /// <summary>Vai usar a obra: o morador, o gerente da loja.</summary>
    Usuario,
}
