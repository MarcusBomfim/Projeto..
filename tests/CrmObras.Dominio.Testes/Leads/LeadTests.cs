using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using static CrmObras.Dominio.Testes.Leads.LeadsDeTeste;

namespace CrmObras.Dominio.Testes.Leads;

public sealed class LeadTests
{
    [Fact]
    public void Chega_novo_com_o_que_o_interessado_deixou()
    {
        var lead = DoSite();

        Assert.Equal(
            ("Marina Costa", "Logística Rota Sul Ltda", "Logística Rota Sul Ltda", SituacaoDoLead.Novo, OrigemDoLead.Site, Segunda),
            (lead.Nome, lead.Empresa, lead.NomeDoCliente, lead.Situacao, lead.Origem, lead.ChegouEm));
        Assert.Equal(Avaliacao.NaoFeita, lead.Avaliacao);
    }

    [Fact]
    public void Quem_procura_para_si_e_o_proprio_cliente()
    {
        var lead = DoSite(new DadosDoLead(" Paulo Mendes ", " ", Telefone: Telefone.De("(19) 99111-2222")));

        Assert.Equal(("Paulo Mendes", (string?)null, "Paulo Mendes"), (lead.Nome, lead.Empresa, lead.NomeDoCliente));
    }

    [Fact]
    public void Diz_todos_os_problemas_do_lead_de_uma_vez()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Lead(new DadosDoLead(""), OrigemDoLead.Indicacao, " ", Segunda));

        Assert.Equal(
            "Falta o nome de quem procurou. O lead precisa de um e-mail ou de um telefone: sem um dos dois, não há como responder. Lead de indicação diz quem indicou.",
            erro.Message);
    }

    [Fact]
    public void Evento_diz_qual_e_indicacao_diz_quem()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Lead(DoGalpao(), OrigemDoLead.Evento, null, Segunda));
        Assert.Equal("Lead de evento diz qual evento.", erro.Message);

        var indicado = new Lead(DoGalpao(), OrigemDoLead.Indicacao, "  Arq. Renata Prado ", Segunda);
        Assert.Equal("Arq. Renata Prado", indicado.DetalheDaOrigem);
    }

    [Fact]
    public void Qualifica_depois_da_avaliacao_que_permite()
    {
        var lead = DoSite();

        lead.Avaliar(QueQualifica);
        Assert.Equal(SituacaoDoLead.EmQualificacao, lead.Situacao);

        lead.Qualificar(Segunda.AddDays(2));
        lead.Qualificar(Segunda.AddDays(5));
        Assert.Equal((SituacaoDoLead.Qualificado, (DateTimeOffset?)Segunda.AddDays(2)), (lead.Situacao, lead.QualificadoEm));
    }

    [Fact]
    public void Sem_a_avaliacao_que_permite_diz_o_que_falta()
    {
        var lead = DoSite();
        lead.Avaliar(new Avaliacao(Resposta.NaoSabido, Resposta.Nao, Resposta.Sim, Resposta.NaoSabido));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => lead.Qualificar(Segunda));

        Assert.Equal(
            "O lead Marina Costa ainda não se qualifica: falta o orçamento confirmado e falar com quem decide ou ter o prazo da obra.",
            erro.Message);
        Assert.Equal(SituacaoDoLead.EmQualificacao, lead.Situacao);
    }

    [Fact]
    public void Avaliar_de_novo_o_qualificado_o_devolve_a_qualificacao()
    {
        var lead = Qualificado();

        lead.Avaliar(QueQualifica with { Orcamento = Resposta.Nao });

        Assert.Equal((SituacaoDoLead.EmQualificacao, (DateTimeOffset?)null), (lead.Situacao, lead.QualificadoEm));
    }

    [Fact]
    public void Descarta_com_o_motivo_e_reabre_com_a_avaliacao_que_tinha()
    {
        var lead = DoSite();
        lead.Avaliar(QueQualifica);

        lead.Descartar(MotivoDoDescarte.PrazoDistante, "  Só em 2028 ", Segunda.AddDays(3));
        Assert.Equal(
            (SituacaoDoLead.Descartado, (MotivoDoDescarte?)MotivoDoDescarte.PrazoDistante, "Só em 2028", (DateTimeOffset?)Segunda.AddDays(3)),
            (lead.Situacao, lead.MotivoDoDescarte, lead.ObservacaoDoDescarte, lead.DescartadoEm));

        lead.Reabrir();
        Assert.Equal(
            (SituacaoDoLead.EmQualificacao, (MotivoDoDescarte?)null, QueQualifica),
            (lead.Situacao, lead.MotivoDoDescarte, lead.Avaliacao));
    }

    [Fact]
    public void Descarte_por_outro_motivo_diz_qual()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => DoSite().Descartar(MotivoDoDescarte.Outro, " ", Segunda));

        Assert.Equal("Descarte por outro motivo diz qual, na observação.", erro.Message);
    }

    [Fact]
    public void O_descartado_so_volta_reaberto()
    {
        var lead = DoSite();
        lead.Descartar(MotivoDoDescarte.NaoResponde, null, Segunda);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => lead.Avaliar(QueQualifica));
        Assert.Equal("O lead Marina Costa foi descartado, e não pode ser avaliado: reabra antes.", erro.Message);

        Assert.Throws<ExcecaoDeDominio>(() => lead.Qualificar(Segunda));
        Assert.Throws<ExcecaoDeDominio>(() => lead.Descartar(MotivoDoDescarte.SemInteresse, null, Segunda));
    }

    [Fact]
    public void So_o_descartado_e_reaberto()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Qualificado().Reabrir());

        Assert.Equal("O lead Marina Costa não foi descartado: não há o que reabrir.", erro.Message);
    }
}
