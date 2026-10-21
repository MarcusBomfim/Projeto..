using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Propostas;
using static CrmObras.Dominio.Testes.Propostas.PropostasDeTeste;

namespace CrmObras.Dominio.Testes.Propostas;

public sealed class EnvioTests
{
    private static readonly DateTimeOffset Terca = Segunda.AddDays(1);

    [Fact]
    public void Enviada_vai_para_o_cliente_e_nao_muda_mais()
    {
        var proposta = DoGalpao(Venda());

        proposta.Enviar(Carlos, Alcadas.Padrao, Terca);
        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Redigir(DoGalpao(1m)));

        Assert.Equal(
            (SituacaoDaVersao.Enviada, (Comercial?)Carlos, (DateTimeOffset?)Terca),
            (proposta.Atual.Situacao, proposta.Atual.EnviadaPor, proposta.Atual.EnviadaEm));
        Assert.Same(proposta.Atual, proposta.Vigente);
        Assert.Equal("A versão 1 já foi enviada, e não muda mais: crie uma versão nova.", erro.Message);
        Assert.Equal(0m, proposta.Atual.Desconto);
    }

    [Fact]
    public void Diz_tudo_o_que_impede_o_envio_de_uma_vez()
    {
        var proposta = new Proposta(Venda(), new DadosDaVersao([], 12m, new DateOnly(2026, 10, 19)), Segunda);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, Alcadas.Padrao, Terca));

        Assert.Equal(
            "A versão 1 não tem item: proposta sem item não se envia. "
            + "A versão 1 vale até 19/10/2026, antes do envio: a validade é de hoje em diante. "
            + "O desconto de 12% passa da alçada do vendedor, de 5%: precisa da aprovação do diretor.",
            erro.Message);
        Assert.Equal(SituacaoDaVersao.Rascunho, proposta.Atual.Situacao);
    }

    [Fact]
    public void A_validade_pode_ser_o_proprio_dia_do_envio()
    {
        var proposta = new Proposta(Venda(), DoGalpao() with { ValidaAte = new DateOnly(2026, 10, 20) }, Segunda);

        proposta.Enviar(Carlos, Alcadas.Padrao, Terca);

        Assert.Equal(SituacaoDaVersao.Enviada, proposta.Atual.Situacao);
    }

    [Theory]
    [InlineData("5", Cargo.Vendedor)]
    [InlineData("10", Cargo.Gerente)]
    [InlineData("15", Cargo.Diretor)]
    public void Dentro_da_propria_alcada_ninguem_precisa_aprovar(string desconto, Cargo cargo)
    {
        var proposta = DoGalpao(Venda(), decimal.Parse(desconto, System.Globalization.CultureInfo.InvariantCulture));

        proposta.Enviar(new Comercial("quem.envia", cargo), Alcadas.Padrao, Terca);

        Assert.Equal(SituacaoDaVersao.Enviada, proposta.Atual.Situacao);
    }

    [Fact]
    public void Acima_da_alcada_precisa_de_quem_tem_alcada_para_o_desconto()
    {
        var proposta = DoGalpao(Venda(), 8m);

        var semAprovacao = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, Alcadas.Padrao, Terca));
        proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda.AddHours(3));
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca);

        Assert.Equal("O desconto de 8% passa da alçada do vendedor, de 5%: precisa da aprovação do gerente.", semAprovacao.Message);
        Assert.Equal(new AprovacaoDeDesconto(Helena, Segunda.AddHours(3)), proposta.Atual.Aprovacao);
        Assert.Equal(SituacaoDaVersao.Enviada, proposta.Atual.Situacao);
    }

    [Fact]
    public void Quem_aprova_precisa_de_alcada_para_o_desconto()
    {
        var proposta = DoGalpao(Venda(), 12.5m);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda));
        proposta.AprovarDesconto(Roberto, Alcadas.Padrao, Segunda);

        Assert.Equal("O gerente aprova até 10%: o desconto de 12,5% é do diretor.", erro.Message);
        Assert.Equal(Roberto, proposta.Atual.Aprovacao?.Aprovador);
    }

    [Fact]
    public void Acima_da_alcada_do_diretor_nao_ha_quem_aprove()
    {
        var proposta = DoGalpao(Venda(), 20m);

        var aprovar = Assert.Throws<ExcecaoDeDominio>(() => proposta.AprovarDesconto(Roberto, Alcadas.Padrao, Segunda));
        var enviar = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Roberto, Alcadas.Padrao, Terca));

        Assert.Equal("O desconto de 20% passa da maior alçada, de 15% do diretor: não há quem aprove.", aprovar.Message);
        Assert.Equal(aprovar.Message, enviar.Message);
    }

    [Fact]
    public void Sem_desconto_nao_ha_o_que_aprovar()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => DoGalpao(Venda()).AprovarDesconto(Roberto, Alcadas.Padrao, Segunda));

        Assert.Equal("A versão 1 não tem desconto: não há o que aprovar.", erro.Message);
    }

    [Fact]
    public void Redigir_de_novo_desfaz_a_aprovacao()
    {
        var proposta = DoGalpao(Venda(), 8m);
        proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda);

        proposta.Redigir(DoGalpao(8m) with { Itens = [Piso, Estrutura] });
        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, Alcadas.Padrao, Terca));

        Assert.Null(proposta.Atual.Aprovacao);
        Assert.Equal("O desconto de 8% passa da alçada do vendedor, de 5%: precisa da aprovação do gerente.", erro.Message);
    }

    [Fact]
    public void A_alcada_e_conferida_no_envio()
    {
        var proposta = DoGalpao(Venda(), 8m);
        proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda);
        var reduzidas = new Alcadas(3m, 6m, 12m);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, reduzidas, Terca));

        Assert.Equal("O desconto de 8% passa da alçada do vendedor, de 3%: precisa da aprovação do diretor.", erro.Message);
    }

    [Fact]
    public void A_versao_nova_comeca_igual_a_ultima_e_substitui_a_enviada_quando_vai()
    {
        var proposta = Enviada(5m);
        var primeira = proposta.Atual;

        var segunda = proposta.NovaVersao();
        Assert.Same(primeira, proposta.Vigente);
        proposta.Redigir(DoGalpao(4m));
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca.AddDays(5));

        Assert.Equal((2, SituacaoDaVersao.Enviada), (segunda.Numero, segunda.Situacao));
        Assert.Equal(SituacaoDaVersao.Substituida, primeira.Situacao);
        Assert.Same(segunda, proposta.Vigente);
        Assert.Equal([1, 2], proposta.Versoes.Select(versao => versao.Numero));
    }

    [Fact]
    public void A_versao_nova_copia_o_conteudo_mas_nao_a_aprovacao_nem_o_envio()
    {
        var proposta = DoGalpao(Venda(), 8m);
        proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda);
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca);
        var primeira = proposta.Atual;

        var segunda = proposta.NovaVersao();

        Assert.Equal(primeira.Itens, segunda.Itens);
        Assert.Equal(
            (primeira.Desconto, primeira.ValidaAte, primeira.CondicoesDePagamento, primeira.Total),
            (segunda.Desconto, segunda.ValidaAte, segunda.CondicoesDePagamento, segunda.Total));
        Assert.Equal(
            (SituacaoDaVersao.Rascunho, (AprovacaoDeDesconto?)null, (Comercial?)null, (DateTimeOffset?)null),
            (segunda.Situacao, segunda.Aprovacao, segunda.EnviadaPor, segunda.EnviadaEm));
    }

    [Fact]
    public void So_ha_um_rascunho_por_vez()
    {
        var proposta = DoGalpao(Venda());

        var erro = Assert.Throws<ExcecaoDeDominio>(() => proposta.NovaVersao());

        Assert.Equal("A versão 1 ainda é rascunho: redija nela.", erro.Message);
    }

    [Fact]
    public void So_o_rascunho_se_aprova_e_se_envia()
    {
        var proposta = Enviada(4m);

        var aprovar = Assert.Throws<ExcecaoDeDominio>(() => proposta.AprovarDesconto(Roberto, Alcadas.Padrao, Terca));
        var enviar = Assert.Throws<ExcecaoDeDominio>(() => proposta.Enviar(Carlos, Alcadas.Padrao, Terca));

        Assert.Equal("A versão 1 já foi enviada, e não muda mais: crie uma versão nova.", aprovar.Message);
        Assert.Equal(aprovar.Message, enviar.Message);
    }

    [Fact]
    public void O_login_de_quem_age_e_obrigatorio()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Comercial(" ", Cargo.Gerente));
        var longo = Assert.Throws<ExcecaoDeDominio>(() => new Comercial(new string('l', 101), Cargo.Gerente));

        Assert.Equal("Falta o login de quem age na proposta.", erro.Message);
        Assert.Equal("O login de quem age na proposta passa de 100 caracteres.", longo.Message);
        Assert.Equal("helena.martins", new Comercial(" helena.martins ", Cargo.Gerente).ToString());
    }
}
