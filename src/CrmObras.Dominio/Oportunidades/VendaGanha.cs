using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Oportunidades;

/// <summary>
/// A venda foi ganha: a obra foi vendida, e a partir daqui é do planejamento
/// e da execução (ADR-0016). Vale para a venda ganha direto e para a ganha
/// pelo aceite da proposta.
/// </summary>
/// <param name="Titulo">A obra, como a venda se chamava quando foi ganha.</param>
/// <param name="ValorFechado">Por quanto a obra foi vendida.</param>
/// <param name="GanhaEm">Quando a venda foi ganha.</param>
public sealed record VendaGanha(OportunidadeId Oportunidade, ContaId Conta, string Titulo, Dinheiro ValorFechado, DateTimeOffset GanhaEm)
    : EventoDeDominio
{
    public override Identificador Agregado => Oportunidade;
}
