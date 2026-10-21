using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Propostas;

/// <summary>
/// O cliente aceitou a proposta: a venda é ganha pelo total da versão aceita.
/// </summary>
/// <remarks>
/// Os dois lados são conferidos antes de qualquer mudança — a proposta tem uma
/// versão com o cliente, dentro da validade; a venda está aberta, no funil
/// dela —, e só então a venda é ganha e a proposta, aceita. Não há como ficar
/// com a proposta aceita e a venda aberta, nem o contrário.
/// </remarks>
public static class FechamentoPelaProposta
{
    public static void Aceitar(Proposta proposta, Oportunidade oportunidade, Funil funil, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(proposta);
        ArgumentNullException.ThrowIfNull(oportunidade);

        if (proposta.Oportunidade != oportunidade.Id)
        {
            throw new ExcecaoDeDominio($"A proposta não é da oportunidade {oportunidade.Titulo}.");
        }

        var versao = proposta.ConferirAceite(em);
        oportunidade.Ganhar(funil, versao.Total, em);
        proposta.Aceitar(em);
    }
}
