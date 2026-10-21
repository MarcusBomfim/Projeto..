using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;

namespace CrmObras.Dominio.Testes.Funis;

public sealed class EtapasDoFunilTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private Etapa Visita => _funil.Abertas.Single(etapa => etapa.Nome == "Visita técnica");

    private Etapa Proposta => _funil.Abertas.Single(etapa => etapa.Nome == "Proposta enviada");

    private string[] Ordem => [.. _funil.Abertas.Select(etapa => etapa.Nome)];

    [Fact]
    public void A_etapa_nova_entra_no_lugar_que_a_probabilidade_indica()
    {
        var credito = _funil.AdicionarEtapa(new("Aprovação de crédito", 70, 20, Requisitos.QuemDecide));
        _funil.AdicionarEtapa(new("Levantamento", 20));
        _funil.AdicionarEtapa(new("Prospecção", 5));
        _funil.AdicionarEtapa(new("Assinatura", 95));

        Assert.Equal(
            ["Prospecção", "Primeiro contato", "Visita técnica", "Levantamento", "Orçamento em elaboração", "Proposta enviada", "Aprovação de crédito", "Negociação", "Assinatura"],
            Ordem);
        Assert.Equal(("Aprovação de crédito", 70, (int?)20, Requisitos.QuemDecide), (credito.Nome, credito.Probabilidade, credito.DiasEsperados, credito.Requisitos));
        Assert.Equal("Prospecção", _funil.Primeira.Nome);
    }

    [Fact]
    public void Recusa_etapa_nova_com_nome_que_ja_existe_sem_contar_maiusculas()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AdicionarEtapa(new("VISITA TÉCNICA", 30)));

        Assert.Equal("Já existe a etapa Visita técnica.", erro.Message);
        Assert.Equal("Já existe a etapa Ganha.", Assert.Throws<ExcecaoDeDominio>(() => _funil.AdicionarEtapa(new("ganha", 30))).Message);
    }

    [Fact]
    public void Diz_tudo_o_que_esta_errado_na_etapa_nova_de_uma_vez()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AdicionarEtapa(new("Negociação", 100, 0)));

        Assert.Equal(
            "A probabilidade da etapa Negociação vai de 1% a 99%: 0% é perder, e 100%, ganhar. "
            + "O tempo esperado da etapa Negociação vai de 1 a 365 dias. "
            + "Já existe a etapa Negociação.",
            erro.Message);
        Assert.Equal(5, _funil.Abertas.Count);
    }

    [Fact]
    public void A_etapa_alterada_fica_no_lugar()
    {
        _funil.AlterarEtapa(Visita.Id, new("  Visita à obra ", 30, 10, Requisitos.QuemDecide));

        var visita = _funil.Abertas[1];
        Assert.Equal(("Visita à obra", 30, (int?)10, Requisitos.QuemDecide), (visita.Nome, visita.Probabilidade, visita.DiasEsperados, visita.Requisitos));
        Assert.Equal(Requisitos.QuemDecide, _funil.RequisitosPara(visita.Id));
    }

    [Fact]
    public void Alterar_nao_deixa_a_probabilidade_cair()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AlterarEtapa(Visita.Id, new("Visita técnica", 50)));

        Assert.Equal(
            "Orçamento em elaboração (40%) não pode vir depois de Visita técnica (50%): a probabilidade não cai conforme a venda avança.",
            erro.Message);
        Assert.Equal(20, Visita.Probabilidade);
    }

    [Fact]
    public void Alterar_conserva_o_proprio_nome_e_recusa_o_de_outra_etapa()
    {
        var visita = Visita;

        _funil.AlterarEtapa(visita.Id, new("visita técnica", 20));
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AlterarEtapa(visita.Id, new("Negociação", 20)));

        Assert.Equal("visita técnica", visita.Nome);
        Assert.Equal("Já existe a etapa Negociação.", erro.Message);
    }

    [Fact]
    public void Das_etapas_finais_so_o_nome_muda()
    {
        _funil.RenomearEtapa(_funil.Ganha.Id, "  Contrato assinado ");
        _funil.RenomearEtapa(_funil.Perdida.Id, "Não fechou");
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AlterarEtapa(_funil.Ganha.Id, new("Contrato assinado", 100)));

        Assert.Equal(("Contrato assinado", "Não fechou"), (_funil.Ganha.Nome, _funil.Perdida.Nome));
        Assert.Equal(
            "Contrato assinado é etapa final: a probabilidade é sempre 100%, sem prazo nem requisito, e só o nome muda.",
            erro.Message);
    }

    [Fact]
    public void Renomear_confere_o_nome()
    {
        _funil.RenomearEtapa(Visita.Id, "Visita à obra");

        Assert.Equal("Visita à obra", _funil.Abertas[1].Nome);
        Assert.Equal("Falta o nome da etapa.", Assert.Throws<ExcecaoDeDominio>(() => _funil.RenomearEtapa(Proposta.Id, " ")).Message);
        Assert.Equal(
            "Já existe a etapa Perdida.",
            Assert.Throws<ExcecaoDeDominio>(() => _funil.RenomearEtapa(Proposta.Id, "perdida")).Message);
        Assert.Equal(
            "O nome da etapa passa de 60 caracteres.",
            Assert.Throws<ExcecaoDeDominio>(() => _funil.RenomearEtapa(Proposta.Id, new string('p', 61))).Message);
    }

    [Fact]
    public void Move_entre_etapas_de_mesma_probabilidade()
    {
        var levantamento = _funil.AdicionarEtapa(new("Levantamento", 20));

        _funil.MoverEtapa(levantamento.Id, 2);

        Assert.Equal(["Primeiro contato", "Levantamento", "Visita técnica", "Orçamento em elaboração", "Proposta enviada", "Negociação"], Ordem);
    }

    [Fact]
    public void Mover_nao_deixa_a_probabilidade_cair()
    {
        var negociacao = _funil.Abertas[^1];

        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.MoverEtapa(negociacao.Id, 2));

        Assert.Equal(
            "Visita técnica (20%) não pode vir depois de Negociação (80%): a probabilidade não cai conforme a venda avança.",
            erro.Message);
        Assert.Equal(["Primeiro contato", "Visita técnica", "Orçamento em elaboração", "Proposta enviada", "Negociação"], Ordem);
    }

    [Fact]
    public void Para_trocar_duas_etapas_de_lugar_iguala_se_a_probabilidade_antes()
    {
        var visita = Visita;
        var orcamento = _funil.Abertas[2];

        _funil.AlterarEtapa(orcamento.Id, new(orcamento.Nome, 20, orcamento.DiasEsperados, orcamento.Requisitos));
        _funil.MoverEtapa(orcamento.Id, 2);
        _funil.AlterarEtapa(visita.Id, new(visita.Nome, 40, visita.DiasEsperados));

        Assert.Equal(["Primeiro contato 10", "Orçamento em elaboração 20", "Visita técnica 40", "Proposta enviada 60", "Negociação 80"], _funil.Abertas.Select(etapa => $"{etapa} {etapa.Probabilidade}"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Recusa_posicao_que_nao_existe(int posicao)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.MoverEtapa(Visita.Id, posicao));

        Assert.Equal($"Não há a posição {posicao}: as etapas abertas do funil Vendas de obras vão da 1 à 5.", erro.Message);
    }

    [Fact]
    public void Move_para_a_primeira_e_para_a_ultima_posicao()
    {
        var contato = _funil.Primeira;
        _funil.AlterarEtapa(contato.Id, new(contato.Nome, 20, contato.DiasEsperados));

        _funil.MoverEtapa(Visita.Id, 1);
        Assert.Equal("Visita técnica", _funil.Primeira.Nome);

        var negociacao = _funil.Abertas[^1];
        _funil.AlterarEtapa(Proposta.Id, new(Proposta.Nome, 80, Proposta.DiasEsperados, Proposta.Requisitos));
        _funil.MoverEtapa(Proposta.Id, 5);
        Assert.Equal(("Negociação", "Proposta enviada"), (_funil.Abertas[3].Nome, _funil.Abertas[4].Nome));
        Assert.Same(negociacao, _funil.Abertas[3]);
    }

    [Fact]
    public void Etapa_final_nao_se_move_nem_se_arquiva()
    {
        var mover = Assert.Throws<ExcecaoDeDominio>(() => _funil.MoverEtapa(_funil.Ganha.Id, 1));
        var arquivar = Assert.Throws<ExcecaoDeDominio>(() => _funil.ArquivarEtapa(_funil.Perdida.Id));

        Assert.Equal("Ganha é etapa final, e fica sempre no fim do funil.", mover.Message);
        Assert.Equal("Perdida é etapa final, e não é arquivada: toda venda termina ganha ou perdida.", arquivar.Message);
    }

    [Fact]
    public void A_etapa_arquivada_sai_do_funil_e_fica_guardada()
    {
        var visita = Visita;

        _funil.ArquivarEtapa(visita.Id);
        _funil.ArquivarEtapa(visita.Id);

        Assert.True(visita.Arquivada);
        Assert.Equal(["Primeiro contato", "Orçamento em elaboração", "Proposta enviada", "Negociação"], Ordem);
        Assert.Equal([visita], _funil.Arquivadas);
        Assert.Equal(
            "A etapa Visita técnica está arquivada: reative-a antes de mover.",
            Assert.Throws<ExcecaoDeDominio>(() => _funil.MoverEtapa(visita.Id, 1)).Message);
    }

    [Fact]
    public void O_nome_da_arquivada_continua_ocupado()
    {
        _funil.ArquivarEtapa(Visita.Id);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.AdicionarEtapa(new("Visita técnica", 25)));

        Assert.Equal("Já existe a etapa Visita técnica, arquivada: reative-a, ou escolha outro nome.", erro.Message);
    }

    [Fact]
    public void A_ultima_etapa_aberta_nao_se_arquiva()
    {
        var funil = new Funil("Balcão", [new("Atendimento", 50)]);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => funil.ArquivarEtapa(funil.Primeira.Id));

        Assert.Equal(
            "Atendimento é a única etapa aberta do funil Balcão: crie outra antes de arquivá-la, porque é nela que a venda começa.",
            erro.Message);
        Assert.False(funil.Primeira.Arquivada);
    }

    [Fact]
    public void A_primeira_arquivada_passa_o_comeco_para_a_seguinte()
    {
        _funil.ArquivarEtapa(_funil.Primeira.Id);

        Assert.Equal("Visita técnica", _funil.Primeira.Nome);
    }

    [Fact]
    public void A_reativada_volta_no_lugar_que_a_probabilidade_indica()
    {
        var visita = Visita;
        _funil.ArquivarEtapa(visita.Id);
        _funil.AlterarEtapa(visita.Id, new("Visita técnica", 50, 14));
        _funil.AdicionarEtapa(new("Levantamento", 20));

        _funil.ReativarEtapa(visita.Id);

        Assert.False(visita.Arquivada);
        Assert.Empty(_funil.Arquivadas);
        Assert.Equal(["Primeiro contato", "Levantamento", "Orçamento em elaboração", "Visita técnica", "Proposta enviada", "Negociação"], Ordem);
    }

    [Fact]
    public void A_primeira_continua_sem_requisito_ao_adicionar_e_ao_alterar()
    {
        var adicionar = Assert.Throws<ExcecaoDeDominio>(() => _funil.AdicionarEtapa(new("Prospecção", 5, Requisitos: Requisitos.QuemDecide)));
        var alterar = Assert.Throws<ExcecaoDeDominio>(() => _funil.AlterarEtapa(_funil.Primeira.Id, new("Primeiro contato", 10, Requisitos: Requisitos.ValorEstimado)));

        Assert.Equal("Prospecção ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.", adicionar.Message);
        Assert.Equal("Primeiro contato ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.", alterar.Message);
        Assert.Equal((5, Requisitos.Nenhum), (_funil.Abertas.Count, _funil.Primeira.Requisitos));
    }

    [Fact]
    public void A_primeira_continua_sem_requisito_ao_mover_e_ao_arquivar()
    {
        var contato = _funil.Primeira;
        var visita = Visita;
        _funil.AlterarEtapa(contato.Id, new(contato.Nome, 20));
        _funil.AlterarEtapa(visita.Id, new(visita.Nome, 20, Requisitos: Requisitos.QuemDecide));

        var mover = Assert.Throws<ExcecaoDeDominio>(() => _funil.MoverEtapa(visita.Id, 1));
        var arquivar = Assert.Throws<ExcecaoDeDominio>(() => _funil.ArquivarEtapa(contato.Id));

        Assert.Equal("Visita técnica ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.", mover.Message);
        Assert.Equal(mover.Message, arquivar.Message);
        Assert.Equal((contato, false), (_funil.Primeira, contato.Arquivada));
    }

    [Fact]
    public void A_primeira_continua_sem_requisito_ao_reativar()
    {
        var contato = _funil.Primeira;
        _funil.ArquivarEtapa(contato.Id);
        _funil.AlterarEtapa(contato.Id, new(contato.Nome, 10, Requisitos: Requisitos.QuemDecide));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.ReativarEtapa(contato.Id));

        Assert.Equal("Primeiro contato ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.", erro.Message);
        Assert.Equal([contato], _funil.Arquivadas);
    }

    [Fact]
    public void So_a_arquivada_se_reativa()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => _funil.ReativarEtapa(Visita.Id));

        Assert.Equal("A etapa Visita técnica não está arquivada: não há o que reativar.", erro.Message);
    }
}
