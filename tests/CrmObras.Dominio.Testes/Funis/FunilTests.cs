using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;

namespace CrmObras.Dominio.Testes.Funis;

public sealed class FunilTests
{
    [Fact]
    public void O_padrao_da_construtora_vai_do_primeiro_contato_a_negociacao_e_termina_ganho_ou_perdido()
    {
        var funil = Funil.PadraoDaConstrutora();

        Assert.Equal("Vendas de obras", funil.Nome);
        Assert.Equal(
            ["Primeiro contato 10%", "Visita técnica 20%", "Orçamento em elaboração 40%", "Proposta enviada 60%", "Negociação 80%"],
            funil.Abertas.Select(etapa => $"{etapa} {etapa.Probabilidade}%"));
        Assert.All(funil.Abertas, etapa => Assert.Equal((TipoDeEtapa.Aberta, false, false), (etapa.Tipo, etapa.EhFinal, etapa.Arquivada)));
        Assert.Equal("Primeiro contato", funil.Primeira.Nome);
        Assert.Equal((TipoDeEtapa.Ganha, "Ganha", 100, true), (funil.Ganha.Tipo, funil.Ganha.Nome, funil.Ganha.Probabilidade, funil.Ganha.EhFinal));
        Assert.Equal((TipoDeEtapa.Perdida, "Perdida", 0, true), (funil.Perdida.Tipo, funil.Perdida.Nome, funil.Perdida.Probabilidade, funil.Perdida.EhFinal));
        Assert.Empty(funil.Arquivadas);
    }

    [Fact]
    public void Guarda_a_etapa_como_configurada()
    {
        var funil = new Funil(
            "  Obras públicas ",
            [new("  Edital publicado ", 5, 30), new("Habilitação", 30, Requisitos: Requisitos.PrevisaoDeFechamento)]);

        var edital = funil.Primeira;
        var habilitacao = funil.Abertas[1];

        Assert.Equal("Obras públicas", funil.Nome);
        Assert.Equal(("Edital publicado", 5, (int?)30, Requisitos.Nenhum), (edital.Nome, edital.Probabilidade, edital.DiasEsperados, edital.Requisitos));
        Assert.Equal(("Habilitação", 30, (int?)null, Requisitos.PrevisaoDeFechamento), (habilitacao.Nome, habilitacao.Probabilidade, habilitacao.DiasEsperados, habilitacao.Requisitos));
        Assert.NotEqual(funil.Id, Funil.PadraoDaConstrutora().Id);
    }

    [Fact]
    public void Diz_tudo_o_que_esta_errado_de_uma_vez()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil(
            " ",
            [new("", 50), new("Visita", 0, 0), new("visita", 100, 366)]));

        Assert.Equal(
            "Falta o nome do funil. "
            + "Falta o nome da etapa. "
            + "A probabilidade da etapa Visita vai de 1% a 99%: 0% é perder, e 100%, ganhar. "
            + "O tempo esperado da etapa Visita vai de 1 a 365 dias. "
            + "A probabilidade da etapa visita vai de 1% a 99%: 0% é perder, e 100%, ganhar. "
            + "O tempo esperado da etapa visita vai de 1 a 365 dias. "
            + "A etapa visita aparece duas vezes.",
            erro.Message);
    }

    [Theory]
    [InlineData(1, 1, 60)]
    [InlineData(99, 365, 60)]
    public void Aceita_os_limites(int probabilidade, int dias, int tamanhoDoNome)
    {
        var funil = new Funil(new string('f', 100), [new(new string('e', tamanhoDoNome), probabilidade, dias)]);

        Assert.Equal((probabilidade, (int?)dias), (funil.Primeira.Probabilidade, funil.Primeira.DiasEsperados));
    }

    [Fact]
    public void Recusa_nomes_longos_demais()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil(new string('f', 101), [new(new string('e', 61), 10)]));

        Assert.Equal("O nome do funil passa de 100 caracteres. O nome da etapa passa de 60 caracteres.", erro.Message);
    }

    [Fact]
    public void Recusa_funil_sem_etapa_aberta()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil("Vendas", []));

        Assert.Equal("O funil precisa de uma etapa aberta, ao menos: é nela que a venda começa.", erro.Message);
    }

    [Theory]
    [InlineData("Ganha")]
    [InlineData("PERDIDA")]
    public void Ganha_e_Perdida_o_funil_ja_tem(string nome)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil("Vendas", [new("Contato", 10), new(nome, 90)]));

        Assert.Equal($"{nome} é etapa final, e o funil já a tem: a lista é só das etapas abertas.", erro.Message);
    }

    [Fact]
    public void A_probabilidade_nao_cai_conforme_a_venda_avanca()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil(
            "Vendas",
            [new("Contato", 10), new("Proposta", 60), new("Visita", 20), new("Negociação", 80), new("Contrato", 70)]));

        Assert.Equal(
            "Visita (20%) não pode vir depois de Proposta (60%): a probabilidade não cai conforme a venda avança. "
            + "Contrato (70%) não pode vir depois de Negociação (80%): a probabilidade não cai conforme a venda avança.",
            erro.Message);
    }

    [Fact]
    public void A_primeira_etapa_nao_tem_requisito()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Funil(
            "Obras públicas",
            [new("Edital publicado", 5, Requisitos: Requisitos.PrevisaoDeFechamento), new("Habilitação", 30, Requisitos: Requisitos.ValorEstimado)]));

        Assert.Equal(
            "Edital publicado ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.",
            erro.Message);
    }

    [Fact]
    public void Etapas_seguidas_podem_ter_a_mesma_probabilidade()
    {
        var funil = new Funil("Vendas", [new("Visita", 20), new("Levantamento", 20)]);

        Assert.Equal(["Visita", "Levantamento"], funil.Abertas.Select(etapa => etapa.Nome));
    }

    [Fact]
    public void Muda_de_nome()
    {
        var funil = Funil.PadraoDaConstrutora();

        funil.Renomear("  Obras privadas ");
        var erro = Assert.Throws<ExcecaoDeDominio>(() => funil.Renomear(""));

        Assert.Equal("Obras privadas", funil.ToString());
        Assert.Equal("Falta o nome do funil.", erro.Message);
    }

    [Fact]
    public void Os_requisitos_se_acumulam_pular_etapa_nao_pula_a_exigencia()
    {
        var funil = Funil.PadraoDaConstrutora();
        var requisitos = funil.Abertas.Select(etapa => funil.RequisitosPara(etapa.Id)).ToList();

        Assert.Equal(
            [
                Requisitos.Nenhum,
                Requisitos.Nenhum,
                Requisitos.QuemDecide,
                Requisitos.QuemDecide | Requisitos.ValorEstimado | Requisitos.PrevisaoDeFechamento,
                Requisitos.QuemDecide | Requisitos.ValorEstimado | Requisitos.PrevisaoDeFechamento,
            ],
            requisitos);
        Assert.Equal(Requisitos.Nenhum, funil.Abertas[^1].Requisitos);
    }

    [Fact]
    public void Ganhar_e_perder_nao_pedem_requisito_de_etapa()
    {
        var funil = Funil.PadraoDaConstrutora();

        Assert.Equal(Requisitos.Nenhum, funil.RequisitosPara(funil.Ganha.Id));
        Assert.Equal(Requisitos.Nenhum, funil.RequisitosPara(funil.Perdida.Id));
    }

    [Fact]
    public void Etapa_arquivada_nao_recebe_oportunidade_e_some_dos_requisitos()
    {
        var funil = Funil.PadraoDaConstrutora();
        var orcamento = funil.Abertas[2];
        var proposta = funil.Abertas[3];

        funil.ArquivarEtapa(orcamento.Id);
        var erro = Assert.Throws<ExcecaoDeDominio>(() => funil.RequisitosPara(orcamento.Id));

        Assert.Equal("A etapa Orçamento em elaboração está arquivada: nenhuma oportunidade entra nela.", erro.Message);
        Assert.Equal(Requisitos.ValorEstimado | Requisitos.PrevisaoDeFechamento, funil.RequisitosPara(proposta.Id));
    }

    [Fact]
    public void Encontra_qualquer_etapa_pelo_id_e_recusa_a_de_outro_funil()
    {
        var funil = Funil.PadraoDaConstrutora();
        var visita = funil.Abertas[1];
        var deOutro = Funil.PadraoDaConstrutora().Primeira.Id;

        funil.ArquivarEtapa(visita.Id);

        Assert.Same(visita, funil.Etapa(visita.Id));
        Assert.Same(funil.Ganha, funil.Etapa(funil.Ganha.Id));
        Assert.Same(funil.Perdida, funil.Etapa(funil.Perdida.Id));
        Assert.Equal(
            $"O funil Vendas de obras não tem a etapa {deOutro}.",
            Assert.Throws<ExcecaoDeDominio>(() => funil.Etapa(deOutro)).Message);
    }
}
