using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Atividades;

public sealed class AtividadeTests
{
    private static readonly DateTimeOffset Quarta10h = Segunda.AddDays(2).AddHours(1);

    private readonly Conta _rotaSul = RotaSul();

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    [Fact]
    public void Nasce_pendente_na_conta_e_na_venda()
    {
        var galpao = DoGalpao(_funil, _rotaSul);
        var paulo = _rotaSul.Contatos[1];

        var visita = new Atividade(
            _rotaSul,
            new DadosDaAtividade(TipoDeAtividade.Visita, "  Visita ao terreno ", Quarta10h, paulo.Id, "  Levar o topógrafo "),
            " carlos.souza ",
            Segunda,
            galpao);

        Assert.Equal(
            (_rotaSul.Id, (OportunidadeId?)galpao.Id, TipoDeAtividade.Visita, "Visita ao terreno", (ContatoId?)paulo.Id, "Levar o topógrafo"),
            (visita.Conta, visita.Oportunidade, visita.Tipo, visita.Assunto, visita.Com, visita.Detalhes));
        Assert.Equal(
            (Quarta10h, "carlos.souza", Segunda, SituacaoDaAtividade.Pendente, 0),
            (visita.PrevistaPara, visita.Responsavel, visita.CriadaEm, visita.Situacao, visita.Adiamentos));
        Assert.Equal(((string?)null, (string?)null, (DateTimeOffset?)null), (visita.Resultado, visita.MotivoDoCancelamento, visita.EncerradaEm));
        Assert.NotEqual(visita.Id, Ligacao().Id);
    }

    [Fact]
    public void Pode_ser_so_da_conta()
    {
        var ligacao = new Atividade(_rotaSul, new DadosDaAtividade(TipoDeAtividade.Ligacao, "Pós-venda", Quarta10h, Detalhes: " "), "ana.ribeiro", Segunda);

        Assert.Equal(((OportunidadeId?)null, (ContatoId?)null, (string?)null), (ligacao.Oportunidade, ligacao.Com, ligacao.Detalhes));
    }

    [Fact]
    public void Diz_tudo_o_que_esta_errado_de_uma_vez()
    {
        var outra = new Conta("Construtora Alfa");
        var galpao = DoGalpao(_funil, _rotaSul);
        var deFora = ContatoId.Novo();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Atividade(
            outra,
            new DadosDaAtividade(TipoDeAtividade.Reuniao, " ", Quarta10h, deFora, new string('d', 1001)),
            "",
            Segunda,
            galpao));

        Assert.Equal(
            "Falta o assunto da atividade. "
            + "Falta o responsável pela atividade. "
            + "Os detalhes passam de 1000 caracteres. "
            + $"A conta Construtora Alfa não tem o contato {deFora}. "
            + "A oportunidade Galpão de 1.200 m² em Jundiaí não é da conta Construtora Alfa.",
            erro.Message);
    }

    [Fact]
    public void Confere_os_tamanhos_nos_limites()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Atividade(
            _rotaSul,
            new DadosDaAtividade(TipoDeAtividade.Tarefa, new string('a', 201), Quarta10h),
            new string('r', 101),
            Segunda));
        var nosLimites = new Atividade(
            _rotaSul,
            new DadosDaAtividade(TipoDeAtividade.Tarefa, new string('a', 200), Quarta10h, Detalhes: new string('d', 1000)),
            new string('r', 100),
            Segunda);

        Assert.Equal("O assunto da atividade passa de 200 caracteres. O responsável pela atividade passa de 100 caracteres.", erro.Message);
        Assert.Equal((200, 100, 1000), (nosLimites.Assunto.Length, nosLimites.Responsavel.Length, nosLimites.Detalhes?.Length));
    }

    [Fact]
    public void Venda_fechada_nao_recebe_atividade_nova()
    {
        var galpao = DoGalpao(_funil, _rotaSul);
        galpao.Perder(_funil, new Perda(MotivoDaPerda.Prazo), Segunda);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Ligacao(galpao));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí está fechada: a atividade nova é da conta, e não da venda.", erro.Message);
    }

    [Fact]
    public void Atrasa_quando_a_hora_passa_e_ainda_esta_pendente()
    {
        var ligacao = Ligacao();

        Assert.False(ligacao.EstaAtrasada(Quarta10h));
        Assert.True(ligacao.EstaAtrasada(Quarta10h.AddMinutes(1)));

        ligacao.Concluir("Pediu a proposta por e-mail", Quarta10h.AddMinutes(30));
        Assert.False(ligacao.EstaAtrasada(Quarta10h.AddDays(10)));
    }

    [Fact]
    public void Concluir_pede_o_resultado()
    {
        var ligacao = Ligacao();

        var semResultado = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Concluir("  ", Quarta10h));
        var longo = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Concluir(new string('r', 1001), Quarta10h));
        ligacao.Concluir("  Pediu a proposta por e-mail ", Quarta10h.AddMinutes(30));

        Assert.Equal("Concluir a atividade Retorno do orçamento pede o resultado: o que foi dito, o que ficou combinado.", semResultado.Message);
        Assert.Equal("O resultado passa de 1000 caracteres.", longo.Message);
        Assert.Equal(
            (SituacaoDaAtividade.Concluida, "Pediu a proposta por e-mail", (DateTimeOffset?)Quarta10h.AddMinutes(30)),
            (ligacao.Situacao, ligacao.Resultado, ligacao.EncerradaEm));
    }

    [Fact]
    public void Concluir_antes_da_hora_vale()
    {
        var ligacao = Ligacao();

        var outra = Ligacao();

        ligacao.Concluir("Ele ligou antes", Segunda);
        outra.Concluir(new string('r', 1000), Segunda);

        Assert.Equal(SituacaoDaAtividade.Concluida, ligacao.Situacao);
        Assert.Equal(1000, outra.Resultado?.Length);
    }

    [Fact]
    public void Cancelar_pede_o_motivo()
    {
        var ligacao = Ligacao();

        var semMotivo = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Cancelar("", Quarta10h));
        var longo = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Cancelar(new string('m', 1001), Quarta10h));
        ligacao.Cancelar(" Cliente em férias ", Segunda.AddHours(2));
        var outra = Ligacao();
        outra.Cancelar(new string('m', 1000), Segunda);

        Assert.Equal(1000, outra.MotivoDoCancelamento?.Length);
        Assert.Equal("Cancelar a atividade Retorno do orçamento pede o motivo.", semMotivo.Message);
        Assert.Equal("O motivo passa de 1000 caracteres.", longo.Message);
        Assert.Equal(
            (SituacaoDaAtividade.Cancelada, "Cliente em férias", (DateTimeOffset?)Segunda.AddHours(2), (string?)null),
            (ligacao.Situacao, ligacao.MotivoDoCancelamento, ligacao.EncerradaEm, ligacao.Resultado));
    }

    [Fact]
    public void Nao_termina_antes_de_ter_sido_marcada()
    {
        var ligacao = Ligacao();

        var concluir = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Concluir("Feita", Segunda.AddMinutes(-1)));
        var cancelar = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Cancelar("Não precisa", Segunda.AddMinutes(-1)));

        Assert.Equal("A atividade Retorno do orçamento não pode ser concluída antes de ter sido marcada.", concluir.Message);
        Assert.Equal("A atividade Retorno do orçamento não pode ser cancelada antes de ter sido marcada.", cancelar.Message);
        Assert.Equal(SituacaoDaAtividade.Pendente, ligacao.Situacao);
    }

    [Fact]
    public void Adiar_conta_e_antecipar_nao()
    {
        var ligacao = Ligacao();

        ligacao.Reagendar(Quarta10h.AddDays(1), Segunda.AddHours(1));
        ligacao.Reagendar(Quarta10h.AddDays(3), Segunda.AddHours(2));
        ligacao.Reagendar(Quarta10h, Segunda.AddHours(3));
        ligacao.Reagendar(Quarta10h, Segunda.AddHours(4));

        Assert.Equal((Quarta10h, 2), (ligacao.PrevistaPara, ligacao.Adiamentos));
    }

    [Fact]
    public void Reagenda_para_depois_de_agora()
    {
        var ligacao = Ligacao();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => ligacao.Reagendar(Quarta10h.AddDays(1), Quarta10h.AddDays(1)));

        Assert.Equal("A atividade Retorno do orçamento se reagenda para depois de agora: para antes, ela já nasceria atrasada.", erro.Message);
        Assert.Equal((Quarta10h, 0), (ligacao.PrevistaPara, ligacao.Adiamentos));
    }

    [Fact]
    public void Passa_para_outro_responsavel()
    {
        var ligacao = Ligacao();

        ligacao.TransferirPara(" ana.ribeiro ");

        Assert.Equal("ana.ribeiro", ligacao.Responsavel);
        Assert.Equal("Falta o responsável pela atividade.", Assert.Throws<ExcecaoDeDominio>(() => ligacao.TransferirPara(" ")).Message);
    }

    [Fact]
    public void Encerrada_nao_muda_mais()
    {
        var concluida = Ligacao();
        concluida.Concluir("Feita", Quarta10h);
        var cancelada = Ligacao();
        cancelada.Cancelar("Não precisa", Quarta10h);
        var jaConcluida = "A atividade Retorno do orçamento já foi concluída, e não muda mais.";
        var jaCancelada = "A atividade Retorno do orçamento foi cancelada, e não muda mais: marque outra.";

        Assert.Equal(jaConcluida, Assert.Throws<ExcecaoDeDominio>(() => concluida.Reagendar(Quarta10h.AddDays(1), Quarta10h)).Message);
        Assert.Equal(jaConcluida, Assert.Throws<ExcecaoDeDominio>(() => concluida.TransferirPara("ana.ribeiro")).Message);
        Assert.Equal(jaConcluida, Assert.Throws<ExcecaoDeDominio>(() => concluida.Concluir("De novo", Quarta10h)).Message);
        Assert.Equal(jaCancelada, Assert.Throws<ExcecaoDeDominio>(() => cancelada.Cancelar("De novo", Quarta10h)).Message);
        Assert.Equal(jaCancelada, Assert.Throws<ExcecaoDeDominio>(() => cancelada.Concluir("Feita", Quarta10h)).Message);
    }

    private Atividade Ligacao(Oportunidade? oportunidade = null) => new(
        _rotaSul,
        new DadosDaAtividade(TipoDeAtividade.Ligacao, "Retorno do orçamento", Quarta10h),
        "carlos.souza",
        Segunda,
        oportunidade);
}
