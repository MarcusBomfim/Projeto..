using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using static CrmObras.Dominio.Testes.Propostas.PropostasDeTeste;

namespace CrmObras.Dominio.Testes.Propostas;

public sealed class PropostaTests
{
    [Fact]
    public void Nasce_com_a_primeira_versao_em_rascunho()
    {
        var venda = Venda();

        var proposta = new Proposta(venda, DoGalpao(7.5m) with { CondicoesDePagamento = "  À vista " }, Segunda);
        var versao = Assert.Single(proposta.Versoes);

        Assert.Equal((venda.Id, venda.Conta, Segunda), (proposta.Oportunidade, proposta.Conta, proposta.CriadaEm));
        Assert.Same(versao, proposta.Atual);
        Assert.Equal((1, SituacaoDaVersao.Rascunho, 7.5m, Validade, "À vista"), (versao.Numero, versao.Situacao, versao.Desconto, versao.ValidaAte, versao.CondicoesDePagamento));
        Assert.Equal(((VersaoDaProposta?)null, (VersaoDaProposta?)null), (proposta.Vigente, proposta.Aceita));
        Assert.Equal(
            ((AprovacaoDeDesconto?)null, (Comercial?)null, (DateTimeOffset?)null, (DateTimeOffset?)null, (string?)null),
            (versao.Aprovacao, versao.EnviadaPor, versao.EnviadaEm, versao.RespondidaEm, versao.MotivoDaRecusa));
        Assert.NotEqual(proposta.Id, DoGalpao(venda).Id);
    }

    [Fact]
    public void Soma_as_linhas_e_tira_o_desconto()
    {
        var versao = DoGalpao(Venda(), 7.5m).Atual;

        Assert.Equal(
            ["Piso industrial em concreto armado, fck 30 MPa, 15 cm: R$ 222.600,00", "Estrutura metálica do galpão: R$ 1.450.000,00", "Cobertura em telha termoacústica: R$ 421.740,00"],
            versao.Itens.Select(item => $"{item.Descricao}: {item.Total}"));
        Assert.Equal(
            (Dinheiro.Reais(2_094_340m), Dinheiro.Reais(157_075.50m), Dinheiro.Reais(1_937_264.50m)),
            (versao.Subtotal, versao.ValorDoDesconto, versao.Total));
    }

    [Fact]
    public void Cada_linha_e_o_desconto_vao_ao_centavo()
    {
        var versao = new Proposta(
            Venda(),
            new DadosDaVersao(
                [
                    new("Rejunte", 0.5m, "kg", Dinheiro.Reais(0.01m)),
                    new("Selante", 1.5m, "kg", Dinheiro.Reais(0.01m)),
                    new("Pintura", 12.345m, "m²", Dinheiro.Reais(10.01m)),
                ],
                7.5m,
                Validade),
            Segunda).Atual;

        // 0,005 vai para o par, 0,00; 0,015 vai para o par, 0,02; 123,57345 desce para 123,57.
        Assert.Equal([0m, 0.02m, 123.57m], versao.Itens.Select(item => item.Total.Valor));
        // 7,5% de 123,59 é 9,26925: sobe para 9,27.
        Assert.Equal((Dinheiro.Reais(123.59m), Dinheiro.Reais(9.27m), Dinheiro.Reais(114.32m)), (versao.Subtotal, versao.ValorDoDesconto, versao.Total));
    }

    [Fact]
    public void Guarda_a_linha_como_informada()
    {
        var item = new Proposta(Venda(), new DadosDaVersao([new("  Piso polido ", 10m, " m² ", Dinheiro.Reais(80m))], 0m, Validade), Segunda).Atual.Itens[0];

        Assert.Equal(("Piso polido", 10m, "m²", Dinheiro.Reais(80m)), (item.Descricao, item.Quantidade, item.Unidade, item.PrecoUnitario));
    }

    [Fact]
    public void O_rascunho_pode_comecar_sem_item()
    {
        var versao = new Proposta(Venda(), new DadosDaVersao([], 0m, Validade, " "), Segunda).Atual;

        Assert.Equal((0, Dinheiro.Zero, (string?)null), (versao.Itens.Count, versao.Total, versao.CondicoesDePagamento));
    }

    [Fact]
    public void Diz_tudo_o_que_esta_errado_de_uma_vez()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Proposta(
            Venda(),
            new DadosDaVersao(
                [
                    Piso,
                    new(" ", 0m, "", Dinheiro.Zero),
                    new(new string('d', 501), -1m, "metro quadrado", Dinheiro.Reais(10.005m)),
                ],
                12.345m,
                Validade,
                new string('c', 1001)),
            Segunda));

        Assert.Equal(
            "Item 2: falta a descrição. "
            + "Item 2: a quantidade precisa ser maior que zero. "
            + "Item 2: falta a unidade. "
            + "Item 2: o preço unitário precisa ser maior que zero. "
            + "Item 3: a descrição passa de 500 caracteres. "
            + "Item 3: a quantidade precisa ser maior que zero. "
            + "Item 3: a unidade passa de 10 caracteres. "
            + "Item 3: o preço unitário vai até o centavo: R$ 10,005 tem casas demais. "
            + "O desconto vai de 0% a menos de 100%, com até duas casas: 12,345% não serve. "
            + "As condições de pagamento passam de 1000 caracteres.",
            erro.Message);
    }

    [Fact]
    public void Aceita_os_limites()
    {
        var versao = new Proposta(
            Venda(),
            new DadosDaVersao([new(new string('d', 500), 0.0001m, new string('u', 10), Dinheiro.Reais(0.01m))], 99.99m, Validade, new string('c', 1000)),
            Segunda).Atual;

        Assert.Equal((500, 10, 99.99m, 1000), (versao.Itens[0].Descricao.Length, versao.Itens[0].Unidade.Length, versao.Desconto, versao.CondicoesDePagamento?.Length));
    }

    [Theory]
    [InlineData("100")]
    [InlineData("-0.01")]
    public void O_desconto_fica_entre_0_e_menos_de_100(string desconto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => DoGalpao(Venda(), decimal.Parse(desconto, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.StartsWith("O desconto vai de 0% a menos de 100%", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Venda_fechada_nao_recebe_proposta()
    {
        var funil = Funil.PadraoDaConstrutora();
        var venda = new Oportunidade(new Conta("Rota Sul").Id, "Galpão", funil, "carlos.souza", Segunda);
        venda.Perder(funil, new Perda(MotivoDaPerda.Preco), Segunda);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => DoGalpao(venda));

        Assert.Equal("A oportunidade Galpão está fechada: proposta é para venda em andamento.", erro.Message);
    }

    [Fact]
    public void O_rascunho_muda_a_vontade()
    {
        var proposta = DoGalpao(Venda());

        proposta.Redigir(new DadosDaVersao([Piso], 3m, Validade.AddDays(15)));

        Assert.Equal((1, 3m, Validade.AddDays(15), (string?)null), (proposta.Atual.Itens.Count, proposta.Atual.Desconto, proposta.Atual.ValidaAte, proposta.Atual.CondicoesDePagamento));
        Assert.Single(proposta.Versoes);
    }

    [Fact]
    public void Redigir_confere_o_conteudo_e_nao_muda_nada_se_ha_problema()
    {
        var proposta = DoGalpao(Venda());

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Redigir(new DadosDaVersao([Piso with { Unidade = "" }], 0m, Validade)));

        Assert.Equal("Item 1: falta a unidade.", erro.Message);
        Assert.Equal(3, proposta.Atual.Itens.Count);
    }
}
