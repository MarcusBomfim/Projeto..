using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class OportunidadeTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Nasce_aberta_na_primeira_etapa_do_funil()
    {
        var oportunidade = new Oportunidade(_rotaSul.Id, "  Galpão em Jundiaí ", _funil, " carlos.souza ", Segunda);

        Assert.Equal(
            ("Galpão em Jundiaí", "carlos.souza", _rotaSul.Id, _funil.Id, Segunda),
            (oportunidade.Titulo, oportunidade.Responsavel, oportunidade.Conta, oportunidade.Funil, oportunidade.CriadaEm));
        Assert.Equal((SituacaoDaOportunidade.Aberta, _funil.Primeira.Id, Segunda), (oportunidade.Situacao, oportunidade.Etapa, oportunidade.EntrouNaEtapaEm));
        Assert.Equal([new PassagemPorEtapa(_funil.Primeira.Id, Segunda)], oportunidade.Passagens);
        Assert.Equal(
            ((Dinheiro?)null, (DateOnly?)null, (ContatoId?)null, (Dinheiro?)null, (Perda?)null, (DateTimeOffset?)null),
            (oportunidade.ValorEstimado, oportunidade.PrevisaoDeFechamento, oportunidade.QuemDecide, oportunidade.ValorFechado, oportunidade.Perda, oportunidade.FechadaEm));
        Assert.Null(oportunidade.Lead);
        Assert.NotEqual(oportunidade.Id, DoGalpao(_funil, _rotaSul).Id);
    }

    [Fact]
    public void Lembra_o_lead_de_onde_veio()
    {
        var lead = LeadId.Novo();

        var oportunidade = new Oportunidade(_rotaSul.Id, "Galpão em Jundiaí", _funil, "carlos.souza", Segunda, lead);

        Assert.Equal(lead, oportunidade.Lead);
    }

    [Fact]
    public void Diz_tudo_o_que_falta_de_uma_vez()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Oportunidade(_rotaSul.Id, " ", _funil, "", Segunda));

        Assert.Equal("Falta o título da oportunidade. Falta o responsável pela oportunidade.", erro.Message);
    }

    [Fact]
    public void Confere_o_tamanho_do_titulo_e_do_responsavel()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Oportunidade(_rotaSul.Id, new string('t', 201), _funil, new string('r', 101), Segunda));
        var nosLimites = new Oportunidade(_rotaSul.Id, new string('t', 200), _funil, new string('r', 100), Segunda);

        Assert.Equal("O título da oportunidade passa de 200 caracteres. O responsável pela oportunidade passa de 100 caracteres.", erro.Message);
        Assert.Equal((200, 100), (nosLimites.Titulo.Length, nosLimites.Responsavel.Length));
    }

    [Fact]
    public void Muda_de_titulo_e_de_vendedor()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        oportunidade.Renomear("  Galpão logístico em Jundiaí ");
        oportunidade.TransferirPara(" ana.ribeiro ");

        Assert.Equal(("Galpão logístico em Jundiaí", "ana.ribeiro"), (oportunidade.ToString(), oportunidade.Responsavel));
        Assert.Equal("Falta o título da oportunidade.", Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Renomear("")).Message);
        Assert.Equal("Falta o responsável pela oportunidade.", Assert.Throws<ExcecaoDeDominio>(() => oportunidade.TransferirPara(" ")).Message);
    }

    [Fact]
    public void A_venda_fechada_ainda_muda_de_vendedor()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        oportunidade.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Segunda.AddDays(30));

        oportunidade.TransferirPara("ana.ribeiro");
        oportunidade.Renomear("Galpão logístico");

        Assert.Equal(("ana.ribeiro", "Galpão logístico"), (oportunidade.Responsavel, oportunidade.Titulo));
    }

    [Fact]
    public void Guarda_o_valor_estimado_a_previsao_e_quem_decide()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var paulo = _rotaSul.Contatos[1];

        oportunidade.Estimar(Dinheiro.Reais(2_400_000.50m));
        oportunidade.PreverFechamento(Dezembro);
        oportunidade.DefinirQuemDecide(_rotaSul, paulo.Id);

        Assert.Equal(
            ((Dinheiro?)Dinheiro.Reais(2_400_000.50m), (DateOnly?)Dezembro, (ContatoId?)paulo.Id),
            (oportunidade.ValorEstimado, oportunidade.PrevisaoDeFechamento, oportunidade.QuemDecide));
    }

    [Theory]
    [InlineData("0", "O valor estimado precisa ser maior que zero.")]
    [InlineData("-10", "O valor estimado precisa ser maior que zero.")]
    [InlineData("1500.005", "O valor estimado vai até o centavo: R$ 1.500,005 tem casas demais.")]
    public void O_valor_estimado_e_positivo_e_em_centavos(string valor, string mensagem)
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Estimar(Dinheiro.Reais(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Equal(mensagem, erro.Message);
        Assert.Null(oportunidade.ValorEstimado);
    }

    [Fact]
    public void Quem_decide_e_um_contato_da_conta_da_oportunidade()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var outra = new Conta("Construtora Alfa");
        var bruno = outra.AdicionarContato(new DadosDoContato("Bruno Lima", Email.De("bruno@alfa.com.br")));

        var deOutraConta = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.DefinirQuemDecide(outra, bruno.Id));
        var deFora = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.DefinirQuemDecide(_rotaSul, bruno.Id));

        Assert.Equal(
            "A oportunidade Galpão de 1.200 m² em Jundiaí não é da conta Construtora Alfa: quem decide é um contato do cliente dela.",
            deOutraConta.Message);
        Assert.Equal($"A conta Logística Rota Sul Ltda não tem o contato {bruno.Id}.", deFora.Message);
        Assert.Null(oportunidade.QuemDecide);
    }

    [Fact]
    public void So_anda_pelo_proprio_funil()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var outro = new Funil("Obras públicas", [new("Edital publicado", 5), new("Habilitação", 30)]);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(outro, outro.Abertas[1].Id, Segunda));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí não anda pelo funil Obras públicas.", erro.Message);
        Assert.Equal(erro.Message, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Ganhar(outro, Dinheiro.Reais(1m), Segunda)).Message);
        Assert.Equal(erro.Message, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.EstaParada(outro, Segunda)).Message);
    }
}
