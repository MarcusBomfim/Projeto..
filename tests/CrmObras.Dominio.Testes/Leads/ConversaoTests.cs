using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using static CrmObras.Dominio.Testes.Leads.LeadsDeTeste;

namespace CrmObras.Dominio.Testes.Leads;

public sealed class ConversaoTests
{
    private static readonly Cnpj CnpjDaRotaSul = Cnpj.De("11.222.333/0001-81");

    [Fact]
    public void O_qualificado_vira_conta_nova_com_ele_de_contato_principal()
    {
        var lead = Qualificado(DoGalpao(CnpjDaRotaSul));

        var conta = lead.ConverterEmContaNova([], Segunda.AddDays(4));

        Assert.Equal(("Logística Rota Sul Ltda", (Documento?)CnpjDaRotaSul), (conta.Nome, conta.Documento));
        var marina = Assert.Single(conta.Contatos);
        Assert.Equal(
            ("Marina Costa", "marina.costa@rotasul.com.br", "(11) 99876-5432", PapelNaCompra.NaoSabido),
            (marina.Nome, marina.Email?.Endereco, marina.Telefone?.Formatado, marina.Papel));
        Assert.Equal(marina, conta.Principal);
        Assert.Equal(
            (SituacaoDoLead.Convertido, conta.Id, marina.Id, (DateTimeOffset?)Segunda.AddDays(4)),
            (lead.Situacao, lead.Conta, lead.Contato, lead.ConvertidoEm));
    }

    [Fact]
    public void Nao_vira_conta_nova_se_ja_existe_conta_com_o_mesmo_documento()
    {
        var existente = new Conta("Rota Sul Logística", CnpjDaRotaSul);
        var lead = Qualificado(DoGalpao(CnpjDaRotaSul));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => lead.ConverterEmContaNova(BuscaDeDuplicados.Do(lead, [existente]), Segunda));

        Assert.Equal("Já existe a conta Rota Sul Logística com o documento 11.222.333/0001-81: converta o lead nela.", erro.Message);
        Assert.Equal(SituacaoDoLead.Qualificado, lead.Situacao);
    }

    [Fact]
    public void Indicio_fraco_nao_impede_a_conta_nova_quem_converte_decidiu()
    {
        var homonima = new Conta("Logística Rota Sul");
        var lead = Qualificado();

        var duplicados = BuscaDeDuplicados.Do(lead, [homonima]);
        var conta = lead.ConverterEmContaNova(duplicados, Segunda);

        Assert.Equal(Semelhanca.MesmoNome, Assert.Single(duplicados).MaisForte);
        Assert.NotEqual(homonima.Id, conta.Id);
    }

    [Fact]
    public void Na_conta_que_ja_existe_vira_um_contato_novo_e_completa_o_documento()
    {
        var conta = new Conta("Rota Sul Logística");
        conta.AdicionarContato(new DadosDoContato("Otávio Reis", Email.De("otavio@rotasul.com.br"), Papel: PapelNaCompra.Decisor));
        var lead = Qualificado(DoGalpao(CnpjDaRotaSul));

        var marina = lead.ConverterNaConta(conta, Segunda.AddDays(4));

        Assert.Equal(["Otávio Reis", "Marina Costa"], conta.Contatos.Select(contato => contato.Nome));
        Assert.Equal("Otávio Reis", conta.Principal?.Nome);
        Assert.Equal(CnpjDaRotaSul, conta.Documento);
        Assert.Equal((conta.Id, marina.Id), (lead.Conta, lead.Contato));
    }

    [Fact]
    public void Se_o_email_ja_e_de_um_contato_da_conta_o_lead_e_esse_contato()
    {
        var conta = new Conta("Rota Sul Logística");
        var marina = conta.AdicionarContato(new DadosDoContato("Marina C.", Email.De("MARINA.COSTA@rotasul.com.br")));
        var lead = Qualificado();

        var contato = lead.ConverterNaConta(conta, Segunda);

        Assert.Same(marina, contato);
        Assert.Single(conta.Contatos);
    }

    [Fact]
    public void Nao_vira_contato_de_conta_com_outro_documento()
    {
        var conta = new Conta("Outra Construtora", Cnpj.De("12.ABC.345/01DE-35"));
        var lead = Qualificado(DoGalpao(CnpjDaRotaSul));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => lead.ConverterNaConta(conta, Segunda));

        Assert.Equal(
            "O lead é do cliente com o documento 11.222.333/0001-81, e a conta Outra Construtora tem o documento 12.ABC.345/01DE-35: não são o mesmo cliente.",
            erro.Message);
        Assert.Empty(conta.Contatos);
    }

    [Fact]
    public void So_o_qualificado_e_convertido_e_so_uma_vez()
    {
        var naoQualificado = DoSite();
        var erro = Assert.Throws<ExcecaoDeDominio>(() => naoQualificado.ConverterEmContaNova([], Segunda));
        Assert.Equal("O lead Marina Costa ainda não foi qualificado, e não pode ser convertido.", erro.Message);

        var convertido = Qualificado();
        convertido.ConverterEmContaNova([], Segunda);
        var deNovo = Assert.Throws<ExcecaoDeDominio>(() => convertido.ConverterEmContaNova([], Segunda));
        Assert.Equal("O lead Marina Costa já virou cliente, e não pode ser convertido: a história segue na conta.", deNovo.Message);
        Assert.Throws<ExcecaoDeDominio>(() => convertido.Descartar(MotivoDoDescarte.Duplicado, null, Segunda));
    }
}
