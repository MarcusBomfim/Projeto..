using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class FechamentoTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Ganha_pelo_valor_fechado_de_qualquer_etapa_aberta()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        oportunidade.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Segunda.AddDays(20));

        Assert.Equal(
            (SituacaoDaOportunidade.Ganha, _funil.Ganha.Id, (Dinheiro?)Dinheiro.Reais(2_350_000m), (DateTimeOffset?)Segunda.AddDays(20)),
            (oportunidade.Situacao, oportunidade.Etapa, oportunidade.ValorFechado, oportunidade.FechadaEm));
        Assert.Equal(new PassagemPorEtapa(_funil.Primeira.Id, Segunda, Segunda.AddDays(20)), oportunidade.Passagens[0]);
    }

    [Theory]
    [InlineData("0", "O valor fechado precisa ser maior que zero.")]
    [InlineData("99.999", "O valor fechado vai até o centavo: R$ 99,999 tem casas demais.")]
    public void O_valor_fechado_e_positivo_e_em_centavos(string valor, string mensagem)
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Ganhar(_funil, Dinheiro.Reais(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)), Segunda));

        Assert.Equal(mensagem, erro.Message);
        Assert.Equal(SituacaoDaOportunidade.Aberta, oportunidade.Situacao);
    }

    [Fact]
    public void A_venda_ganha_nao_muda_mais_nem_se_reabre()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        oportunidade.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Segunda.AddDays(20));
        var fechada = "A oportunidade Galpão de 1.200 m² em Jundiaí foi ganha: a venda está fechada, e não muda mais.";

        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Estimar(Dinheiro.Reais(1m))).Message);
        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.PreverFechamento(Dezembro)).Message);
        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.DefinirQuemDecide(_rotaSul, _rotaSul.Contatos[0].Id)).Message);
        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, _funil.Primeira.Id, Segunda.AddDays(21))).Message);
        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Ganhar(_funil, Dinheiro.Reais(1m), Segunda.AddDays(21))).Message);
        Assert.Equal(fechada, Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Perder(_funil, new Perda(MotivoDaPerda.Preco), Segunda.AddDays(21))).Message);
        Assert.Equal(
            "A oportunidade Galpão de 1.200 m² em Jundiaí foi ganha, e venda ganha não se reabre: virou obra.",
            Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Reabrir(_funil, _funil.Primeira.Id, Segunda.AddDays(21))).Message);
    }

    [Fact]
    public void Perde_com_o_motivo()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.Preco, "  Construtora Beta ", " Ficou 8% abaixo "), Segunda.AddDays(15));

        Assert.Equal(
            (SituacaoDaOportunidade.Perdida, _funil.Perdida.Id, (DateTimeOffset?)Segunda.AddDays(15)),
            (oportunidade.Situacao, oportunidade.Etapa, oportunidade.FechadaEm));
        Assert.Equal(new Perda(MotivoDaPerda.Preco, "Construtora Beta", "Ficou 8% abaixo"), oportunidade.Perda);
        Assert.Null(oportunidade.ValorFechado);
    }

    [Fact]
    public void Diz_tudo_o_que_falta_na_perda_de_uma_vez()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        var concorrente = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Perder(_funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, " ", new string('o', 1001)), Segunda));
        var outro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Perder(_funil, new Perda(MotivoDaPerda.Outro, new string('c', 201)), Segunda));

        Assert.Equal("Perda para o concorrente diz qual concorrente. A observação passa de 1000 caracteres.", concorrente.Message);
        Assert.Equal("Perda por outro motivo diz qual, na observação. O nome do concorrente passa de 200 caracteres.", outro.Message);
        Assert.Equal(SituacaoDaOportunidade.Aberta, oportunidade.Situacao);
    }

    [Fact]
    public void Aceita_os_textos_da_perda_nos_limites()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, new string('c', 200), new string('o', 1000)), Segunda);

        Assert.Equal((200, 1000), (oportunidade.Perda?.Concorrente?.Length, oportunidade.Perda?.Observacao?.Length));
    }

    [Fact]
    public void A_perdida_so_muda_depois_de_reaberta()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.SemResposta), Segunda.AddDays(15));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Estimar(Dinheiro.Reais(10m)));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí foi perdida: reabra antes de mudá-la.", erro.Message);
    }

    [Fact]
    public void A_perdida_reabre_numa_etapa_aberta_e_esquece_a_perda()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var proposta = _funil.Chamada("Proposta enviada");
        oportunidade.MoverPara(_funil, proposta.Id, Segunda.AddDays(5));
        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.Prazo), Segunda.AddDays(15));

        oportunidade.Reabrir(_funil, proposta.Id, Segunda.AddDays(40));

        Assert.Equal(
            (SituacaoDaOportunidade.Aberta, proposta.Id, (Perda?)null, (DateTimeOffset?)null),
            (oportunidade.Situacao, oportunidade.Etapa, oportunidade.Perda, oportunidade.FechadaEm));
        Assert.Equal(
            [_funil.Primeira.Id, proposta.Id, _funil.Perdida.Id, proposta.Id],
            oportunidade.Passagens.Select(passagem => passagem.Etapa));
    }

    [Fact]
    public void Reabrir_confere_a_etapa_como_mover()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.SemCredito), Segunda.AddDays(15));

        var semRequisito = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Reabrir(_funil, _funil.Chamada("Orçamento em elaboração").Id, Segunda.AddDays(20)));
        var final = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Reabrir(_funil, _funil.Ganha.Id, Segunda.AddDays(20)));
        var antes = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Reabrir(_funil, _funil.Primeira.Id, Segunda.AddDays(10)));

        Assert.Equal("Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Orçamento em elaboração, falta quem decide.", semRequisito.Message);
        Assert.Equal("Ganha é etapa final: a venda chega nela ganha, com o valor fechado.", final.Message);
        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí não pode sair da etapa antes de ter entrado nela.", antes.Message);
        Assert.Equal(SituacaoDaOportunidade.Perdida, oportunidade.Situacao);
    }

    [Fact]
    public void So_a_perdida_se_reabre()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.Reabrir(_funil, _funil.Primeira.Id, Segunda));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí não foi perdida: não há o que reabrir.", erro.Message);
    }
}
