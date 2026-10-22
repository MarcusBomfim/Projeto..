using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class ContaReconstituidaTests
{
    private static readonly Cnpj CnpjDaRotaSul = Cnpj.De("11.222.333/0001-81");

    private static readonly DadosDoContato Marina = new("Marina Costa", Email.De("marina@rotasul.com.br"), Telefone.De("(19) 99876-5432"), "Compradora", PapelNaCompra.Influenciador);

    private static readonly DadosDoContato Sergio = new("Sérgio Lima", Email.De("sergio@rotasul.com.br"), Papel: PapelNaCompra.Decisor);

    [Fact]
    public void Volta_com_a_identidade_os_contatos_e_o_principal()
    {
        var conta = new Conta("Rota Sul Logística", CnpjDaRotaSul);
        conta.AdicionarContato(Marina);
        var sergio = conta.AdicionarContato(Sergio);
        conta.DefinirPrincipal(sergio.Id);

        var refeita = Conta.Reconstituir(conta.Id, conta.Nome, conta.Documento, ContatosDe(conta), sergio.Id);

        Assert.Equal((conta.Id, "Rota Sul Logística", (Documento?)CnpjDaRotaSul), (refeita.Id, refeita.Nome, refeita.Documento));
        Assert.Equal(ContatosDe(conta), ContatosDe(refeita));
        Assert.Equal(sergio.Id, refeita.Principal?.Id);
    }

    [Fact]
    public void A_conta_sem_contato_volta_sem_principal()
    {
        var id = ContaId.Novo();

        var refeita = Conta.Reconstituir(id, "Rota Sul", documento: null, [], principal: null);

        Assert.Equal((id, (Contato?)null), (refeita.Id, refeita.Principal));
        Assert.Empty(refeita.Contatos);
    }

    [Fact]
    public void Os_contatos_passam_pela_conferencia_de_quem_os_adiciona()
    {
        var marina = ContatoId.Novo();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(
            ContaId.Novo(),
            "Rota Sul",
            documento: null,
            [(marina, Marina), (ContatoId.Novo(), Sergio with { Email = Marina.Email })],
            marina));

        Assert.Equal("O e-mail marina@rotasul.com.br já é do contato Marina Costa.", erro.Message);
    }

    [Fact]
    public void O_mesmo_contato_nao_volta_duas_vezes()
    {
        var marina = ContatoId.Novo();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(ContaId.Novo(), "Rota Sul", null, [(marina, Marina), (marina, Sergio)], marina));

        Assert.Equal($"A conta Rota Sul está gravada com o contato {marina} duas vezes.", erro.Message);
    }

    [Fact]
    public void Contato_e_principal_vem_juntos()
    {
        var marina = ContatoId.Novo();
        var deFora = ContatoId.Novo();

        var semPrincipal = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(ContaId.Novo(), "Rota Sul", null, [(marina, Marina)], principal: null));
        var semContato = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(ContaId.Novo(), "Rota Sul", null, [], marina));
        var principalDeFora = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(ContaId.Novo(), "Rota Sul", null, [(marina, Marina)], deFora));

        Assert.Equal("A conta Rota Sul está gravada com contatos, e nenhum é o principal.", semPrincipal.Message);
        Assert.Equal($"A conta Rota Sul está gravada sem contatos, e com o principal {marina}.", semContato.Message);
        Assert.Equal($"A conta Rota Sul não tem o contato {deFora}.", principalDeFora.Message);
    }

    [Fact]
    public void O_nome_passa_pela_regra_de_sempre()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Conta.Reconstituir(ContaId.Novo(), " ", null, [], null));

        Assert.Equal("Falta o nome da conta.", erro.Message);
    }

    private static List<(ContatoId Id, DadosDoContato Dados)> ContatosDe(Conta conta) =>
        [.. conta.Contatos.Select(contato => (contato.Id, new DadosDoContato(contato.Nome, contato.Email, contato.Telefone, contato.Cargo, contato.Papel)))];
}
