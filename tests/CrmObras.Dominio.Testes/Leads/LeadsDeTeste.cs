using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes.Leads;

/// <summary>Os leads dos testes: o do galpão, que chega pelo site, e as avaliações de costume.</summary>
internal static class LeadsDeTeste
{
    public static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    public static readonly Avaliacao QueQualifica = new(Resposta.Sim, Resposta.Sim, Resposta.Sim, Resposta.NaoSabido);

    public static DadosDoLead DoGalpao(Documento? documento = null) => new(
        "Marina Costa",
        "Logística Rota Sul Ltda",
        documento,
        Email.De("marina.costa@rotasul.com.br"),
        Telefone.De("(11) 99876-5432"),
        "Galpão de 1.200 m² em Jundiaí, para começar em março");

    public static Lead DoSite(DadosDoLead? dados = null) => new(dados ?? DoGalpao(), OrigemDoLead.Site, null, Segunda);

    public static Lead Qualificado(DadosDoLead? dados = null)
    {
        var lead = DoSite(dados);
        lead.Avaliar(QueQualifica);
        lead.Qualificar(Segunda.AddDays(2));

        return lead;
    }
}
