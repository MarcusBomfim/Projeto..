using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class AndamentoNoFunilTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Anda_de_etapa_em_etapa_e_guarda_cada_passagem()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var visita = _funil.Chamada("Visita técnica");
        var orcamento = _funil.Chamada("Orçamento em elaboração");

        oportunidade.MoverPara(_funil, visita.Id, Segunda.AddDays(3));
        oportunidade.MoverPara(_funil, orcamento.Id, Segunda.AddDays(10));

        Assert.Equal((orcamento.Id, Segunda.AddDays(10)), (oportunidade.Etapa, oportunidade.EntrouNaEtapaEm));
        Assert.Equal(
            [
                new PassagemPorEtapa(_funil.Primeira.Id, Segunda, Segunda.AddDays(3)),
                new PassagemPorEtapa(visita.Id, Segunda.AddDays(3), Segunda.AddDays(10)),
                new PassagemPorEtapa(orcamento.Id, Segunda.AddDays(10)),
            ],
            oportunidade.Passagens);
    }

    [Fact]
    public void Nao_entra_na_etapa_sem_o_que_ela_pede()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var orcamento = _funil.Chamada("Orçamento em elaboração");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, orcamento.Id, Segunda.AddDays(1)));

        Assert.Equal("Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Orçamento em elaboração, falta quem decide.", erro.Message);
        Assert.Equal(_funil.Primeira.Id, oportunidade.Etapa);
        Assert.Single(oportunidade.Passagens);
    }

    [Fact]
    public void Pular_etapa_nao_pula_a_exigencia()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var negociacao = _funil.Chamada("Negociação");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, negociacao.Id, Segunda.AddDays(1)));

        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Negociação, falta o valor estimado, a previsão de fechamento e quem decide.",
            erro.Message);
    }

    [Fact]
    public void Diz_so_o_que_falta()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        oportunidade.DefinirQuemDecide(_rotaSul, _rotaSul.Contatos[1].Id);
        oportunidade.PreverFechamento(Dezembro);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, _funil.Chamada("Proposta enviada").Id, Segunda.AddDays(1)));

        Assert.Equal("Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Proposta enviada, falta o valor estimado.", erro.Message);
    }

    [Fact]
    public void Com_tudo_o_que_as_etapas_pedem_pula_direto_para_a_negociacao()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var negociacao = _funil.Chamada("Negociação");

        oportunidade.MoverPara(_funil, negociacao.Id, Segunda.AddDays(1));

        Assert.Equal(negociacao.Id, oportunidade.Etapa);
    }

    [Fact]
    public void Volta_para_uma_etapa_anterior()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var proposta = _funil.Chamada("Proposta enviada");
        var orcamento = _funil.Chamada("Orçamento em elaboração");

        oportunidade.MoverPara(_funil, proposta.Id, Segunda.AddDays(1));
        oportunidade.MoverPara(_funil, orcamento.Id, Segunda.AddDays(8));

        Assert.Equal(orcamento.Id, oportunidade.Etapa);
        Assert.Equal(3, oportunidade.Passagens.Count);
    }

    [Fact]
    public void Mover_para_onde_ja_esta_nao_muda_nada()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        oportunidade.MoverPara(_funil, _funil.Primeira.Id, Segunda.AddDays(5));

        Assert.Equal([new PassagemPorEtapa(_funil.Primeira.Id, Segunda)], oportunidade.Passagens);
    }

    [Fact]
    public void Nao_entra_em_etapa_arquivada_mas_sai_dela()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var visita = _funil.Chamada("Visita técnica");
        oportunidade.MoverPara(_funil, visita.Id, Segunda.AddDays(1));
        _funil.ArquivarEtapa(visita.Id);

        var outra = DoGalpao(_funil, _rotaSul);
        var erro = Assert.Throws<ExcecaoDeDominio>(() => outra.MoverPara(_funil, visita.Id, Segunda.AddDays(2)));
        oportunidade.MoverPara(_funil, _funil.Chamada("Orçamento em elaboração").Id, Segunda.AddDays(2));

        Assert.Equal("A etapa Visita técnica está arquivada: nenhuma oportunidade entra nela.", erro.Message);
        Assert.Equal(_funil.Chamada("Orçamento em elaboração").Id, oportunidade.Etapa);
    }

    [Fact]
    public void A_Ganha_e_a_Perdida_tem_caminho_proprio()
    {
        var oportunidade = Completa(_funil, _rotaSul);

        var ganha = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, _funil.Ganha.Id, Segunda.AddDays(1)));
        var perdida = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, _funil.Perdida.Id, Segunda.AddDays(1)));

        Assert.Equal("Ganha é etapa final: a venda chega nela ganha, com o valor fechado.", ganha.Message);
        Assert.Equal("Perdida é etapa final: a venda chega nela perdida, com o motivo.", perdida.Message);
        Assert.Equal(SituacaoDaOportunidade.Aberta, oportunidade.Situacao);
    }

    [Fact]
    public void Nao_sai_da_etapa_antes_de_ter_entrado()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var visita = _funil.Chamada("Visita técnica");
        oportunidade.MoverPara(_funil, visita.Id, Segunda.AddDays(3));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, _funil.Primeira.Id, Segunda.AddDays(2)));
        oportunidade.MoverPara(_funil, _funil.Primeira.Id, Segunda.AddDays(3));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí não pode sair da etapa antes de ter entrado nela.", erro.Message);
        Assert.Equal(TimeSpan.Zero, oportunidade.Passagens[1].Duracao(Segunda.AddDays(9)));
    }

    [Fact]
    public void Etapa_que_nao_e_do_funil_e_recusada()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var deOutro = Funil.PadraoDaConstrutora().Primeira.Id;

        var erro = Assert.Throws<ExcecaoDeDominio>(() => oportunidade.MoverPara(_funil, deOutro, Segunda));

        Assert.Equal($"O funil Vendas de obras não tem a etapa {deOutro}.", erro.Message);
    }
}
