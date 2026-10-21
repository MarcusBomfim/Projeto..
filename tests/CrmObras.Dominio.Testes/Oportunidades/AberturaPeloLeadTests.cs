using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Testes.Leads;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class AberturaPeloLeadTests
{
    private static readonly DateTimeOffset Conversao = LeadsDeTeste.Segunda.AddDays(4);

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    [Fact]
    public void A_venda_nasce_na_conta_do_lead_com_a_obra_que_ele_descreveu()
    {
        var lead = LeadsDeTeste.Qualificado();
        var conta = lead.ConverterEmContaNova([], Conversao);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);

        Assert.Equal(
            (conta.Id, "Galpão de 1.200 m² em Jundiaí, para começar em março", (LeadId?)lead.Id, "carlos.souza", _funil.Primeira.Id),
            (oportunidade.Conta, oportunidade.Titulo, oportunidade.Lead, oportunidade.Responsavel, oportunidade.Etapa));
    }

    [Fact]
    public void Quem_conversa_e_decide_ja_entra_como_quem_decide()
    {
        var lead = LeadsDeTeste.Qualificado();
        Assert.Equal(Resposta.Sim, lead.Avaliacao.FalaComQuemDecide);
        var conta = lead.ConverterEmContaNova([], Conversao);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);
        oportunidade.MoverPara(_funil, _funil.Chamada("Orçamento em elaboração").Id, Conversao.AddDays(7));

        Assert.Equal(lead.Contato, oportunidade.QuemDecide);
    }

    [Fact]
    public void Quem_conversa_e_nao_decide_deixa_quem_decide_em_aberto()
    {
        var lead = LeadsDeTeste.DoSite();
        lead.Avaliar(new Avaliacao(Resposta.Sim, Resposta.Nao, Resposta.Sim, Resposta.Sim));
        lead.Qualificar(Conversao);
        var conta = lead.ConverterEmContaNova([], Conversao);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);

        Assert.Null(oportunidade.QuemDecide);
    }

    [Fact]
    public void Na_conta_que_ja_existia_quem_decide_e_o_contato_reaproveitado()
    {
        var conta = new Conta("Logística Rota Sul");
        var marina = conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina.costa@rotasul.com.br")));
        var lead = LeadsDeTeste.Qualificado();
        lead.ConverterNaConta(conta, Conversao);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);

        Assert.Equal(marina.Id, oportunidade.QuemDecide);
    }

    [Fact]
    public void O_contato_que_saiu_da_conta_nao_e_quem_decide()
    {
        var lead = LeadsDeTeste.Qualificado();
        var conta = lead.ConverterEmContaNova([], Conversao);
        conta.RemoverContato(conta.Contatos[0].Id);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);

        Assert.Null(oportunidade.QuemDecide);
    }

    [Fact]
    public void So_o_lead_convertido_abre_venda()
    {
        var lead = LeadsDeTeste.Qualificado();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => AberturaPeloLead.Abrir(lead, new Conta("Rota Sul"), _funil, "carlos.souza", Conversao));

        Assert.Equal("O lead Marina Costa ainda não virou cliente: converta-o antes de abrir a venda.", erro.Message);
    }

    [Fact]
    public void A_venda_e_aberta_na_conta_em_que_o_lead_virou_cliente()
    {
        var lead = LeadsDeTeste.Qualificado();
        lead.ConverterEmContaNova([], Conversao);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => AberturaPeloLead.Abrir(lead, new Conta("Construtora Alfa"), _funil, "carlos.souza", Conversao));

        Assert.Equal("O lead Marina Costa virou cliente em outra conta, e não na Construtora Alfa.", erro.Message);
    }

    [Fact]
    public void Sem_interesse_descrito_o_titulo_e_a_obra_do_cliente()
    {
        var lead = LeadsDeTeste.Qualificado(LeadsDeTeste.DoGalpao() with { Interesse = null });
        var conta = lead.ConverterEmContaNova([], Conversao);

        var oportunidade = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao);

        Assert.Equal("Obra de Logística Rota Sul Ltda", oportunidade.Titulo);
    }

    [Fact]
    public void O_interesse_longo_e_cortado_na_ultima_palavra_que_cabe()
    {
        var interesse = string.Join(' ', Enumerable.Repeat("galpão", 40));
        var lead = LeadsDeTeste.Qualificado(LeadsDeTeste.DoGalpao() with { Interesse = interesse });
        var conta = lead.ConverterEmContaNova([], Conversao);

        var titulo = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao).Titulo;

        Assert.Equal($"{string.Join(' ', Enumerable.Repeat("galpão", 28))}…", titulo);
        Assert.True(titulo.Length <= 200);
    }

    [Fact]
    public void O_interesse_sem_espaco_e_cortado_no_tamanho()
    {
        var lead = LeadsDeTeste.Qualificado(LeadsDeTeste.DoGalpao() with { Interesse = new string('g', 300) });
        var conta = lead.ConverterEmContaNova([], Conversao);

        var titulo = AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao).Titulo;

        Assert.Equal($"{new string('g', 199)}…", titulo);
    }

    [Fact]
    public void O_interesse_que_cabe_inteiro_fica_inteiro()
    {
        var interesse = new string('g', 200);
        var lead = LeadsDeTeste.Qualificado(LeadsDeTeste.DoGalpao() with { Interesse = interesse });
        var conta = lead.ConverterEmContaNova([], Conversao);

        Assert.Equal(interesse, AberturaPeloLead.Abrir(lead, conta, _funil, "carlos.souza", Conversao).Titulo);
    }
}
