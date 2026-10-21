using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Relatorios;
using static CrmObras.Dominio.Testes.Relatorios.RelatoriosDeTeste;

namespace CrmObras.Dominio.Testes.Relatorios;

public sealed class MotivosDePerdaTests
{
    private static readonly Periodo Outubro = Periodo.Do(Mes.De(2026, 10), Brasilia);

    private readonly Funil _funil = FunilSimples();

    [Fact]
    public void Diz_por_que_para_quem_e_onde_as_vendas_se_perderam()
    {
        var doca = Venda(_funil, "Doca", Em(1, 10), 300_000m).Perdida(_funil, Em(10, 10), MotivoDaPerda.Preco, "Construtora Beta Ltda.");
        var muro = Venda(_funil, "Muro", Em(1, 10), 100_000m).Para(_funil, "Visita", Em(5, 10)).Perdida(_funil, Em(12, 10), MotivoDaPerda.Preco, "construtora beta");
        var galpao = Venda(_funil, "Galpão", Em(1, 10), 500_000m).Para(_funil, "Proposta", Em(5, 10)).Perdida(_funil, Em(15, 10), MotivoDaPerda.ConcorrenteEscolhido, "Alfa Engenharia");
        var semValor = Venda(_funil, "Sem valor", Em(1, 10)).Perdida(_funil, Em(20, 10), MotivoDaPerda.SemResposta);
        var portaria = Venda(_funil, "Portaria", Em(1, 10), 50_000m).Para(_funil, "Visita", Em(5, 10)).Perdida(_funil, Em(25, 10), MotivoDaPerda.Prazo);

        var motivos = MotivosDePerda.Do(_funil, [portaria, galpao, semValor, muro, doca], Outubro);

        Assert.Equal((Outubro, 5, Dinheiro.Reais(950_000m), 1), (motivos.Periodo, motivos.Perdidas, motivos.Valor, motivos.SemValor));
        Assert.Equal(
            [
                (MotivoDaPerda.Preco, 2, Dinheiro.Reais(400_000m)),
                (MotivoDaPerda.ConcorrenteEscolhido, 1, Dinheiro.Reais(500_000m)),
                (MotivoDaPerda.Prazo, 1, Dinheiro.Reais(50_000m)),
                (MotivoDaPerda.SemResposta, 1, Dinheiro.Zero),
            ],
            motivos.PorMotivo.Select(perdas => (perdas.Motivo, perdas.Vendas, perdas.Valor)));
        Assert.Equal(
            [("Construtora Beta Ltda.", 2, Dinheiro.Reais(400_000m)), ("Alfa Engenharia", 1, Dinheiro.Reais(500_000m))],
            motivos.PorConcorrente.Select(perdas => (perdas.Concorrente, perdas.Vendas, perdas.Valor)));
        Assert.Equal(
            [("Contato", 2, Dinheiro.Reais(300_000m)), ("Visita", 2, Dinheiro.Reais(150_000m)), ("Proposta", 1, Dinheiro.Reais(500_000m))],
            motivos.PorEtapa.Select(perdas => (perdas.Etapa.Nome, perdas.Vendas, perdas.Valor)));
    }

    [Fact]
    public void No_empate_vem_primeiro_a_que_mais_valia()
    {
        var pequena = Venda(_funil, "Pequena", Em(1, 10), 10_000m).Perdida(_funil, Em(2, 10), MotivoDaPerda.Prazo, "Construtora Gama");
        var grande = Venda(_funil, "Grande", Em(1, 10), 900_000m).Perdida(_funil, Em(3, 10), MotivoDaPerda.SemCredito, "Construtora Delta");

        var motivos = MotivosDePerda.Do(_funil, [pequena, grande], Outubro);

        Assert.Equal([MotivoDaPerda.SemCredito, MotivoDaPerda.Prazo], motivos.PorMotivo.Select(perdas => perdas.Motivo));
        Assert.Equal(["Construtora Delta", "Construtora Gama"], motivos.PorConcorrente.Select(perdas => perdas.Concorrente));
    }

    [Fact]
    public void Entram_as_vendas_do_funil_que_estao_perdidas_e_se_perderam_no_periodo()
    {
        var setembro = Venda(_funil, "Setembro", Em(1, 9), 1m).Perdida(_funil, Em(30, 9));
        var noveDaNoite = Venda(_funil, "31/10, 22h em Brasília", Em(1, 10), 1m).Perdida(_funil, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        var reaberta = Venda(_funil, "Reaberta", Em(1, 10), 1m).Perdida(_funil, Em(5, 10));
        reaberta.Reabrir(_funil, _funil.Primeira.Id, Em(6, 10));
        var ganha = Venda(_funil, "Ganha", Em(1, 10)).Ganha(_funil, 1m, Em(5, 10));
        var outroFunil = FunilSimples();
        var deOutroFunil = Venda(outroFunil, "De outro funil", Em(1, 10), 1m).Perdida(outroFunil, Em(5, 10));

        var motivos = MotivosDePerda.Do(_funil, [setembro, noveDaNoite, reaberta, ganha, deOutroFunil], Outubro);

        Assert.Equal(1, motivos.Perdidas);
    }

    [Fact]
    public void A_etapa_arquivada_vem_no_fim()
    {
        var naVisita = Venda(_funil, "Na visita", Em(1, 10), 1m).Para(_funil, "Visita", Em(2, 10)).Perdida(_funil, Em(3, 10));
        var naNegociacao = Venda(_funil, "Na negociação", Em(1, 10), 1m).Para(_funil, "Negociação", Em(2, 10)).Perdida(_funil, Em(3, 10));
        var noContato = Venda(_funil, "No contato", Em(1, 10), 1m).Perdida(_funil, Em(3, 10));
        _funil.ArquivarEtapa(_funil.Abertas[1].Id);

        var motivos = MotivosDePerda.Do(_funil, [naVisita, naNegociacao, noContato], Outubro);

        Assert.Equal(["Contato", "Negociação", "Visita"], motivos.PorEtapa.Select(perdas => perdas.Etapa.Nome));
    }

    [Fact]
    public void As_arquivadas_vem_na_ordem_em_que_sairam_do_funil()
    {
        var naProposta = Venda(_funil, "Na proposta", Em(1, 10), 1m).Para(_funil, "Proposta", Em(2, 10)).Perdida(_funil, Em(3, 10));
        var naVisita = Venda(_funil, "Na visita", Em(1, 10), 1m).Para(_funil, "Visita", Em(2, 10)).Perdida(_funil, Em(3, 10));
        _funil.ArquivarEtapa(_funil.Abertas[2].Id);
        _funil.ArquivarEtapa(_funil.Abertas[1].Id);

        var motivos = MotivosDePerda.Do(_funil, [naVisita, naProposta], Outubro);

        Assert.Equal(["Proposta", "Visita"], motivos.PorEtapa.Select(perdas => perdas.Etapa.Nome));
    }

    [Fact]
    public void Sem_perda_no_periodo_as_listas_ficam_vazias()
    {
        var motivos = MotivosDePerda.Do(_funil, [], Outubro);

        Assert.Equal((0, Dinheiro.Zero, 0), (motivos.Perdidas, motivos.Valor, motivos.SemValor));
        Assert.Empty(motivos.PorMotivo);
        Assert.Empty(motivos.PorConcorrente);
        Assert.Empty(motivos.PorEtapa);
    }
}
