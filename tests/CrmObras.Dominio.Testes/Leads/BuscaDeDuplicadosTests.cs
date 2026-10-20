using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using static CrmObras.Dominio.Testes.Leads.LeadsDeTeste;

namespace CrmObras.Dominio.Testes.Leads;

public sealed class BuscaDeDuplicadosTests
{
    private static Conta ComContato(string nome, Documento? documento = null, string? email = null, string? telefone = null)
    {
        var conta = new Conta(nome, documento);
        conta.AdicionarContato(new DadosDoContato(
            "Fulano de Tal",
            email is null ? null : Email.De(email),
            telefone is null ? Telefone.De("(21) 2555-0000") : Telefone.De(telefone)));

        return conta;
    }

    [Fact]
    public void Cada_semelhanca_com_o_seu_motivo()
    {
        var lead = DoSite(DoGalpao(Cnpj.De("11.222.333/0001-81")));

        var mesmoDocumento = ComContato("Rota Sul (matriz)", Cnpj.De("11222333000181"));
        var filial = ComContato("Rota Sul Campinas", Cnpj.De("11.222.333/0002-62"));
        var mesmoEmail = ComContato("Transportes Marina", email: "marina.costa@rotasul.com.br");
        var mesmoTelefone = ComContato("Galpões Jundiaí", telefone: "+55 11 99876-5432");
        var mesmoNome = ComContato("LOGISTICA ROTA SUL");
        var mesmoDominio = ComContato("RS Armazéns", email: "compras@rotasul.com.br");
        var outra = ComContato("Construtora Beta", email: "beta@beta.com.br");

        var duplicados = BuscaDeDuplicados.Do(lead, [outra, mesmoDominio, mesmoNome, mesmoTelefone, mesmoEmail, filial, mesmoDocumento]);

        Assert.Equal(
            [
                ("Rota Sul (matriz)", Semelhanca.MesmoDocumento, "mesmo documento, 11.222.333/0001-81"),
                ("Rota Sul Campinas", Semelhanca.MesmaEmpresa, "a mesma empresa: 11.222.333/0002-62 tem a raiz do CNPJ do lead"),
                ("Transportes Marina", Semelhanca.MesmoEmail, "o e-mail marina.costa@rotasul.com.br é de Fulano de Tal"),
                ("Galpões Jundiaí", Semelhanca.MesmoTelefone, "o telefone (11) 99876-5432 é de Fulano de Tal"),
                ("LOGISTICA ROTA SUL", Semelhanca.MesmoNome, "o mesmo nome, LOGISTICA ROTA SUL"),
                ("RS Armazéns", Semelhanca.MesmoDominio, "Fulano de Tal também tem e-mail em rotasul.com.br"),
            ],
            duplicados.Select(duplicado => (duplicado.NomeDaConta, duplicado.MaisForte, duplicado.Indicios[0].Motivo)));
    }

    [Fact]
    public void A_conta_com_varios_indicios_traz_todos_o_mais_forte_primeiro()
    {
        var lead = DoSite(DoGalpao(Cnpj.De("11.222.333/0001-81")));
        var conta = ComContato("Logística Rota Sul S/A", Cnpj.De("11.222.333/0001-81"), "marina.costa@rotasul.com.br", "(11) 99876-5432");

        var duplicado = Assert.Single(BuscaDeDuplicados.Do(lead, [conta]));

        Assert.Equal(
            [Semelhanca.MesmoDocumento, Semelhanca.MesmoEmail, Semelhanca.MesmoTelefone, Semelhanca.MesmoNome],
            duplicado.Indicios.Select(indicio => indicio.Semelhanca));
        Assert.Equal(conta.Id, duplicado.Conta);
    }

    [Fact]
    public void Provedor_de_email_gratuito_nao_aproxima_ninguem()
    {
        var lead = DoSite(DoGalpao() with { Email = Email.De("marina.costa@gmail.com") });
        var conta = ComContato("Outra Empresa", email: "joao@gmail.com");

        Assert.Empty(BuscaDeDuplicados.Do(lead, [conta]));
    }

    [Fact]
    public void Sem_conta_parecida_nao_ha_duplicado()
    {
        Assert.Empty(BuscaDeDuplicados.Do(DoSite(), [ComContato("Construtora Beta", email: "beta@beta.com.br")]));
        Assert.Empty(BuscaDeDuplicados.Do(DoSite(), []));
    }

    [Theory]
    [InlineData("Construtora Alfa Ltda.", "construtora alfa")]
    [InlineData("CONSTRUTORA ALFA", "construtora alfa")]
    [InlineData("Construtora  Alfa - EPP", "construtora alfa")]
    [InlineData("Construções Alfa S/A", "construcoes alfa")]
    [InlineData("Construções Alfa S.A.", "construcoes alfa")]
    [InlineData("Alfa & Beta Engenharia e Construções", "alfa beta engenharia construcoes")]
    [InlineData("Casa da Árvore", "casa arvore")]
    public void O_nome_se_compara_sem_acento_caixa_pontuacao_e_natureza_juridica(string nome, string comparavel)
    {
        Assert.Equal(comparavel, BuscaDeDuplicados.NomeComparavel(nome));
    }
}
