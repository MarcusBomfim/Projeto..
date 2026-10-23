using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * O banco escolhe as contas que podem ser o cliente do lead — e a busca de
 * duplicados do domínio diz o que cada uma tem em comum com ele (ADR-0003).
 * Cada indício, sozinho, leva a conta para a lista; o que não aproxima
 * ninguém fica de fora.
 */
public sealed class CandidatasADuplicadoTests(BancoDeTestes banco)
{
    private static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    /// <summary>A Marina, da Rota Sul de Campinas: filial de uma matriz de CNPJ 11.222.333/0001-81.</summary>
    private static Lead DaMarina(Email? email = null) => new(
        new DadosDoLead(
            "Marina Costa",
            "Logística Rota Sul Ltda.",
            Cnpj.De("11.222.333/0002-62"),
            email ?? Email.De("marina.costa@rotasul.com.br"),
            Telefone.De("(19) 99876-5432")),
        OrigemDoLead.Site,
        null,
        Segunda);

    [Fact]
    public async Task Cada_indicio_sozinho_traz_a_conta()
    {
        var mesmoDocumento = await Gravar("Rota Sul Campinas", Cnpj.De("11.222.333/0002-62"));
        var mesmaRaiz = await Gravar("Rota Sul Matriz", Cnpj.De("11.222.333/0001-81"));
        var mesmoNome = await Gravar("LOGISTICA ROTA SUL");
        var mesmoEmail = await Gravar("Compras Campinas", contato: new DadosDoContato("Marina", Email.De("marina.costa@rotasul.com.br")));
        var mesmoTelefone = await Gravar("Galpões Campinas", contato: new DadosDoContato("Marina", Telefone: Telefone.De("+55 19 99876-5432")));
        var mesmoDominio = await Gravar("Rota Sul Jundiaí", contato: new DadosDoContato("Otávio", Email.De("otavio@rotasul.com.br")));
        await Gravar("Construtora Alfa", Cnpj.De("12.ABC.345/01DE-35"), new DadosDoContato("Ana", Email.De("ana@alfa.com.br")));

        var candidatas = await _carteira.Contas.CandidatasADuplicado(DaMarina(), Cancelamento);

        Assert.Equal(
            new[] { mesmoNome, mesmoEmail, mesmoTelefone, mesmoDocumento, mesmaRaiz, mesmoDominio }.Select(id => id.Valor).Order(),
            candidatas.Select(conta => conta.Id.Valor).Order());
    }

    [Fact]
    public async Task O_dominio_de_provedor_gratuito_nao_aproxima_ninguem()
    {
        await Gravar("Fulano Reformas", contato: new DadosDoContato("Fulano", Email.De("fulano@gmail.com")));
        var mesmoEmail = await Gravar("Marina Pessoa Física", contato: new DadosDoContato("Marina", Email.De("marina.costa@gmail.com")));

        var candidatas = await _carteira.Contas.CandidatasADuplicado(DaMarina(Email.De("marina.costa@gmail.com")), Cancelamento);

        Assert.Equal([mesmoEmail], candidatas.Select(conta => conta.Id));
    }

    [Fact]
    public async Task A_busca_do_dominio_le_os_indicios_das_candidatas()
    {
        var matriz = await Gravar("Logística Rota Sul", Cnpj.De("11.222.333/0001-81"), new DadosDoContato("Otávio Reis", Email.De("otavio.reis@rotasul.com.br")));
        var lead = DaMarina();

        var duplicados = BuscaDeDuplicados.Do(lead, await _carteira.Contas.CandidatasADuplicado(lead, Cancelamento));

        var duplicado = Assert.Single(duplicados);
        Assert.Equal(matriz, duplicado.Conta);
        Assert.Equal([Semelhanca.MesmaEmpresa, Semelhanca.MesmoNome, Semelhanca.MesmoDominio], duplicado.Indicios.Select(indicio => indicio.Semelhanca));
    }

    [Fact]
    public async Task So_entram_as_contas_da_construtora()
    {
        var outra = new Carteira(banco, Guid.CreateVersion7());
        await outra.Contas.Adicionar(new Conta("Rota Sul Campinas", Cnpj.De("11.222.333/0002-62")), Cancelamento);

        Assert.Empty(await _carteira.Contas.CandidatasADuplicado(DaMarina(), Cancelamento));
    }

    [Fact]
    public async Task O_lead_sem_documento_nem_e_mail_ainda_acha_pelo_nome_e_pelo_telefone()
    {
        var peloNome = await Gravar("Rota Sul S/A");
        var lead = new Lead(new DadosDoLead("Marina", "ROTA SUL", Telefone: Telefone.De("(11) 3333-4444")), OrigemDoLead.Telefone, null, Segunda);

        Assert.Equal([peloNome], (await _carteira.Contas.CandidatasADuplicado(lead, Cancelamento)).Select(conta => conta.Id));
    }

    [Fact]
    public async Task A_conta_renomeada_e_achada_pelo_nome_novo()
    {
        var id = await Gravar("Transportes Campinas");
        var lida = await _carteira.Contas.Obter(id, Cancelamento);
        lida!.Agregado.Renomear("Logística Rota Sul Ltda.");
        await _carteira.Contas.Atualizar(lida.Agregado, lida.Versao, Cancelamento);
        var lead = new Lead(new DadosDoLead("Marina", "LOGISTICA ROTA SUL", Telefone: Telefone.De("(11) 3333-4444")), OrigemDoLead.Telefone, null, Segunda);

        Assert.Equal([id], (await _carteira.Contas.CandidatasADuplicado(lead, Cancelamento)).Select(conta => conta.Id));
    }

    private async Task<ContaId> Gravar(string nome, Documento? documento = null, DadosDoContato? contato = null)
    {
        var conta = new Conta(nome, documento);

        if (contato is not null)
        {
            conta.AdicionarContato(contato);
        }

        await _carteira.Contas.Adicionar(conta, Cancelamento);

        return conta.Id;
    }
}
