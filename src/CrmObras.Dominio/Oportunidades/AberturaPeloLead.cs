using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;

namespace CrmObras.Dominio.Oportunidades;

/// <summary>
/// A venda que nasce do lead convertido: na conta em que ele virou cliente,
/// com a obra que ele descreveu de título, e lembrando de onde veio.
/// </summary>
/// <remarks>
/// Se a qualificação respondeu que quem conversa é quem decide, o contato que
/// o lead virou já entra como quem decide: o orçamento não pede de novo o que
/// a primeira ligação respondeu.
/// </remarks>
public static class AberturaPeloLead
{
    private const int MaiorTitulo = 200;

    public static Oportunidade Abrir(Lead lead, Conta conta, Funil funil, string responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(conta);

        if (lead.Situacao != SituacaoDoLead.Convertido)
        {
            throw new ExcecaoDeDominio($"O lead {lead.Nome} ainda não virou cliente: converta-o antes de abrir a venda.");
        }

        if (lead.Conta != conta.Id)
        {
            throw new ExcecaoDeDominio($"O lead {lead.Nome} virou cliente em outra conta, e não na {conta.Nome}.");
        }

        var oportunidade = new Oportunidade(conta.Id, Titulo(lead.Interesse ?? $"Obra de {conta.Nome}"), funil, responsavel, em, lead.Id);

        if (lead.Avaliacao.FalaComQuemDecide == Resposta.Sim
            && conta.Contatos.FirstOrDefault(contato => contato.Id == lead.Contato) is { } quemDecide)
        {
            oportunidade.DefinirQuemDecide(conta, quemDecide.Id);
        }

        return oportunidade;
    }

    /// <summary>O texto inteiro, se cabe no título; se não, cortado na última palavra que cabe, com reticências.</summary>
    private static string Titulo(string texto)
    {
        if (texto.Length <= MaiorTitulo)
        {
            return texto;
        }

        var corte = texto.LastIndexOf(' ', MaiorTitulo - 1);

        return $"{texto[..(corte > 0 ? corte : MaiorTitulo - 1)].TrimEnd()}…";
    }
}
