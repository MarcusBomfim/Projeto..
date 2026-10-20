using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes.Clientes;

public sealed class ContaTests
{
    private static readonly Cnpj CnpjDaAlfa = Cnpj.De("11.222.333/0001-81");

    private static DadosDoContato Ana(PapelNaCompra papel = PapelNaCompra.Decisor) =>
        new("Ana Ribeiro", Email.De("ana@alfa.com.br"), Telefone.De("(11) 98765-4321"), "Diretora", papel);

    private static DadosDoContato Bruno() => new("Bruno Lima", Email.De("bruno@alfa.com.br"), Cargo: "Engenheiro", Papel: PapelNaCompra.Influenciador);

    [Fact]
    public void Nasce_com_nome_e_identidade_propria_e_o_documento_pode_chegar_depois()
    {
        var conta = new Conta("  Construtora Alfa Ltda ");

        Assert.Equal(("Construtora Alfa Ltda", (Documento?)null), (conta.Nome, conta.Documento));
        Assert.NotEqual(conta.Id, new Conta("Construtora Alfa Ltda").Id);

        conta.InformarDocumento(CnpjDaAlfa);
        Assert.Equal(CnpjDaAlfa, conta.Documento);
    }

    [Theory]
    [InlineData("", "Falta o nome da conta.")]
    [InlineData("   ", "Falta o nome da conta.")]
    public void Recusa_conta_sem_nome(string nome, string mensagem)
    {
        Assert.Equal(mensagem, Assert.Throws<ExcecaoDeDominio>(() => new Conta(nome)).Message);
        Assert.Equal(mensagem, Assert.Throws<ExcecaoDeDominio>(() => new Conta("Alfa").Renomear(nome)).Message);
    }

    [Fact]
    public void Recusa_nome_com_mais_de_200_caracteres()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Conta(new string('a', 201)));

        Assert.Equal("O nome da conta passa de 200 caracteres.", erro.Message);
        Assert.Equal(200, new Conta(new string('a', 200)).Nome.Length);
    }

    [Fact]
    public void O_documento_informado_identifica_a_conta_e_nao_muda()
    {
        var conta = new Conta("Construtora Alfa", CnpjDaAlfa);

        conta.InformarDocumento(Cnpj.De("11222333000181"));
        var erro = Assert.Throws<ExcecaoDeDominio>(() => conta.InformarDocumento(Cnpj.De("11.222.333/0002-62")));

        Assert.Equal(
            "A conta Construtora Alfa já tem o documento 11.222.333/0001-81, e o documento identifica a conta: 11.222.333/0002-62 é de outro cliente.",
            erro.Message);
        Assert.Equal(CnpjDaAlfa, conta.Documento);
    }

    [Fact]
    public void O_primeiro_contato_e_o_principal_ate_que_se_escolha_outro()
    {
        var conta = new Conta("Construtora Alfa");

        var ana = conta.AdicionarContato(Ana());
        var bruno = conta.AdicionarContato(Bruno());
        Assert.Equal(ana, conta.Principal);

        conta.DefinirPrincipal(bruno.Id);
        Assert.Equal(bruno, conta.Principal);
        Assert.Equal(["Ana Ribeiro", "Bruno Lima"], conta.Contatos.Select(contato => contato.Nome));
    }

    [Fact]
    public void Guarda_o_contato_como_informado()
    {
        var conta = new Conta("Construtora Alfa");

        var ana = conta.AdicionarContato(Ana() with { Nome = "  Ana Ribeiro ", Cargo = "  " });

        Assert.Equal(
            ("Ana Ribeiro", (string?)null, "ana@alfa.com.br", "(11) 98765-4321", PapelNaCompra.Decisor),
            (ana.Nome, ana.Cargo, ana.Email?.Endereco, ana.Telefone?.Formatado, ana.Papel));
    }

    [Fact]
    public void Diz_todos_os_problemas_do_contato_de_uma_vez()
    {
        var conta = new Conta("Construtora Alfa");
        conta.AdicionarContato(Ana());

        var erro = Assert.Throws<ExcecaoDeDominio>(() => conta.AdicionarContato(new DadosDoContato(" ")));
        Assert.Equal(
            "Falta o nome do contato. O contato precisa de um e-mail ou de um telefone: sem um dos dois, não há como falar com ele.",
            erro.Message);

        var repetido = Assert.Throws<ExcecaoDeDominio>(() => conta.AdicionarContato(Bruno() with { Email = Email.De("ANA@alfa.com.br") }));
        Assert.Equal("O e-mail ana@alfa.com.br já é do contato Ana Ribeiro.", repetido.Message);
        Assert.Single(conta.Contatos);
    }

    [Fact]
    public void Contato_alterado_pode_manter_o_proprio_email_mas_nao_tomar_o_de_outro()
    {
        var conta = new Conta("Construtora Alfa");
        var ana = conta.AdicionarContato(Ana());
        var bruno = conta.AdicionarContato(Bruno());

        conta.AlterarContato(ana.Id, Ana(PapelNaCompra.Financeiro) with { Cargo = "Diretora financeira" });
        Assert.Equal(("Diretora financeira", PapelNaCompra.Financeiro), (ana.Cargo, ana.Papel));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => conta.AlterarContato(bruno.Id, Bruno() with { Email = ana.Email }));
        Assert.Equal("O e-mail ana@alfa.com.br já é do contato Ana Ribeiro.", erro.Message);
        Assert.Equal("bruno@alfa.com.br", bruno.Email?.Endereco);
    }

    [Fact]
    public void O_principal_so_sai_sozinho()
    {
        var conta = new Conta("Construtora Alfa");
        var ana = conta.AdicionarContato(Ana());
        var bruno = conta.AdicionarContato(Bruno());

        var erro = Assert.Throws<ExcecaoDeDominio>(() => conta.RemoverContato(ana.Id));
        Assert.Equal("Ana Ribeiro é o contato principal da conta Construtora Alfa: escolha outro principal antes de remover.", erro.Message);

        conta.RemoverContato(bruno.Id);
        conta.RemoverContato(ana.Id);
        Assert.Equal((0, (Contato?)null), (conta.Contatos.Count, conta.Principal));

        // A conta que ficou sem ninguém tem de novo um principal no próximo contato.
        var carla = conta.AdicionarContato(new DadosDoContato("Carla Souza", Telefone: Telefone.De("(21) 2555-0000")));
        Assert.Equal(carla, conta.Principal);
    }

    [Fact]
    public void Contato_de_outra_conta_nao_e_encontrado()
    {
        var alfa = new Conta("Construtora Alfa");
        var beta = new Conta("Construtora Beta");
        var deBeta = beta.AdicionarContato(Bruno());

        var erro = Assert.Throws<ExcecaoDeDominio>(() => alfa.DefinirPrincipal(deBeta.Id));

        Assert.Equal($"A conta Construtora Alfa não tem o contato {deBeta.Id}.", erro.Message);
    }
}
