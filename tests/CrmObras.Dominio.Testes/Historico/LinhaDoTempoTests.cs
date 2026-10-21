using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Historico;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Propostas;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Historico;

public sealed class LinhaDoTempoTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Conta_a_historia_do_cliente_do_mais_recente_para_o_mais_antigo()
    {
        var galpao = Completa(_funil, _rotaSul);
        galpao.MoverPara(_funil, _funil.Chamada("Visita técnica").Id, Segunda.AddDays(2));
        galpao.MoverPara(_funil, _funil.Chamada("Orçamento em elaboração").Id, Segunda.AddDays(7));
        var ligacao = Marcar(TipoDeAtividade.Ligacao, "Combinar a visita", galpao);
        ligacao.Concluir("Visita marcada para quinta", Segunda.AddDays(3));
        var proposta = PropostasDeTeste.DoGalpao(galpao);
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Segunda.AddDays(10));
        proposta.Recusar("Caro", Segunda.AddDays(12));
        proposta.NovaVersao();
        proposta.Redigir(PropostasDeTeste.DoGalpao(4m));
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Segunda.AddDays(13));
        FechamentoPelaProposta.Aceitar(proposta, galpao, _funil, Segunda.AddDays(15));

        var linha = LinhaDoTempo.Da(_rotaSul, [_funil], [galpao], [ligacao], [proposta]);

        Assert.Equal(
            [
                "+15 Venda ganha: Galpão de 1.200 m² em Jundiaí, por R$ 2.010.566,40",
                "+15 Proposta aceita: versão 2, R$ 2.010.566,40",
                "+13 Proposta enviada: versão 2, R$ 2.010.566,40, com 4% de desconto",
                "+12 Proposta recusada: versão 1 — Caro",
                "+10 Proposta enviada: versão 1, R$ 2.094.340,00",
                "+7 Galpão de 1.200 m² em Jundiaí: entrou em Orçamento em elaboração",
                "+3 Ligação: Combinar a visita — Visita marcada para quinta",
                "+2 Galpão de 1.200 m² em Jundiaí: entrou em Visita técnica",
                "+0 Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato",
            ],
            linha.Select(acontecimento => $"+{(acontecimento.Em - Segunda).Days} {acontecimento.Descricao}"));
        Assert.All(linha, acontecimento => Assert.Equal(galpao.Id, acontecimento.Oportunidade));
    }

    [Fact]
    public void A_venda_perdida_e_reaberta_diz_cada_vez()
    {
        var escritorio = new Oportunidade(_rotaSul.Id, "Escritório", _funil, "carlos.souza", Segunda);
        escritorio.Perder(_funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, "Construtora Beta"), Segunda.AddDays(5));
        escritorio.Reabrir(_funil, _funil.Primeira.Id, Segunda.AddDays(9));
        escritorio.Perder(_funil, new Perda(MotivoDaPerda.Preco), Segunda.AddDays(20));

        var linha = LinhaDoTempo.Da(_rotaSul, [_funil], [escritorio], [], []);

        Assert.Equal(
            [
                "Venda perdida: Escritório, por preço",
                "Venda reaberta: Escritório, em Primeiro contato",
                "Venda perdida: Escritório",
                "Venda aberta: Escritório, em Primeiro contato",
            ],
            linha.Select(acontecimento => acontecimento.Descricao));
    }

    [Theory]
    [InlineData(MotivoDaPerda.Preco, "por preço")]
    [InlineData(MotivoDaPerda.Prazo, "por prazo")]
    [InlineData(MotivoDaPerda.ConcorrenteEscolhido, "para Construtora Beta")]
    [InlineData(MotivoDaPerda.ProjetoCancelado, "porque o projeto foi cancelado")]
    [InlineData(MotivoDaPerda.SemCredito, "por falta de crédito")]
    [InlineData(MotivoDaPerda.SemResposta, "por falta de resposta")]
    [InlineData(MotivoDaPerda.Outro, "por outro motivo: Mudou de cidade")]
    public void Diz_por_que_a_venda_foi_perdida(MotivoDaPerda motivo, string porque)
    {
        var venda = new Oportunidade(_rotaSul.Id, "Escritório", _funil, "carlos.souza", Segunda);
        venda.Perder(_funil, new Perda(motivo, "Construtora Beta", "Mudou de cidade"), Segunda.AddDays(1));

        var linha = LinhaDoTempo.Da(_rotaSul, [_funil], [venda], [], []);

        Assert.Equal($"Venda perdida: Escritório, {porque}", linha[0].Descricao);
    }

    [Theory]
    [InlineData(TipoDeAtividade.Ligacao, "Ligação")]
    [InlineData(TipoDeAtividade.Email, "E-mail")]
    [InlineData(TipoDeAtividade.Reuniao, "Reunião")]
    [InlineData(TipoDeAtividade.Visita, "Visita")]
    [InlineData(TipoDeAtividade.Tarefa, "Tarefa")]
    public void A_atividade_feita_diz_o_que_foi_e_o_resultado(TipoDeAtividade tipo, string nome)
    {
        var atividade = Marcar(tipo, "Apresentar a empresa");
        atividade.Concluir("Gostaram do portfólio", Segunda.AddDays(1));

        var acontecimento = Assert.Single(LinhaDoTempo.Da(_rotaSul, [_funil], [], [atividade], []));

        Assert.Equal(new Acontecimento(Segunda.AddDays(1), $"{nome}: Apresentar a empresa — Gostaram do portfólio"), acontecimento);
    }

    [Fact]
    public void O_que_nao_aconteceu_nao_entra()
    {
        var pendente = Marcar(TipoDeAtividade.Ligacao, "Ligar amanhã");
        var cancelada = Marcar(TipoDeAtividade.Visita, "Visita desmarcada");
        cancelada.Cancelar("Chuva", Segunda.AddDays(1));
        var rascunho = PropostasDeTeste.DoGalpao(DoGalpao(_funil, _rotaSul));

        var linha = LinhaDoTempo.Da(_rotaSul, [_funil], [], [pendente, cancelada], [rascunho]);

        Assert.Empty(linha);
    }

    [Fact]
    public void So_entra_o_que_e_do_cliente()
    {
        var alfa = new Conta("Construtora Alfa");
        var daAlfa = new Oportunidade(alfa.Id, "Sede da Alfa", _funil, "carlos.souza", Segunda);
        var ligacaoDaAlfa = new Atividade(alfa, new DadosDaAtividade(TipoDeAtividade.Ligacao, "Apresentação", Segunda), "carlos.souza", Segunda);
        ligacaoDaAlfa.Concluir("Feita", Segunda);
        var propostaDaAlfa = PropostasDeTeste.DoGalpao(daAlfa);
        propostaDaAlfa.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Segunda);
        var daRotaSul = DoGalpao(_funil, _rotaSul);

        var linha = LinhaDoTempo.Da(_rotaSul, [_funil], [daAlfa, daRotaSul], [ligacaoDaAlfa], [propostaDaAlfa]);

        Assert.Equal(["Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato"], linha.Select(acontecimento => acontecimento.Descricao));
    }

    [Fact]
    public void Precisa_do_funil_de_cada_venda()
    {
        var galpao = DoGalpao(_funil, _rotaSul);

        var erro = Assert.Throws<ArgumentException>(() => LinhaDoTempo.Da(_rotaSul, [Funil.PadraoDaConstrutora()], [galpao], [], []));

        Assert.StartsWith("Falta o funil da oportunidade Galpão de 1.200 m² em Jundiaí.", erro.Message, StringComparison.Ordinal);
    }

    private Atividade Marcar(TipoDeAtividade tipo, string assunto, Oportunidade? venda = null) =>
        new(_rotaSul, new DadosDaAtividade(tipo, assunto, Segunda.AddDays(1)), "carlos.souza", Segunda, venda);
}
