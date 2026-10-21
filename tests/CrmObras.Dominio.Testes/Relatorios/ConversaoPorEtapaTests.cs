using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Relatorios;
using static CrmObras.Dominio.Testes.Relatorios.RelatoriosDeTeste;

namespace CrmObras.Dominio.Testes.Relatorios;

public sealed class ConversaoPorEtapaTests
{
    private static readonly Periodo Outubro = Periodo.Do(Mes.De(2026, 10), Brasilia);

    private readonly Funil _funil = FunilSimples();

    [Fact]
    public void Conta_quem_chegou_a_cada_etapa_e_o_que_aconteceu_depois()
    {
        var parada = Venda(_funil, "Parada no contato", Em(1, 10));
        var perdidaNaVisita = Venda(_funil, "Perdida na visita", Em(2, 10)).Para(_funil, "Visita", Em(5, 10)).Perdida(_funil, Em(9, 10));
        var pulouAVisita = Venda(_funil, "Pulou a visita", Em(3, 10)).Para(_funil, "Proposta", Em(6, 10)).Perdida(_funil, Em(20, 10));
        var ganhaPassoAPasso = Venda(_funil, "Ganha passo a passo", Em(4, 10))
            .Para(_funil, "Visita", Em(5, 10))
            .Para(_funil, "Proposta", Em(12, 10))
            .Ganha(_funil, 100_000m, Em(21, 10));
        var ganhaNaNegociacao = Venda(_funil, "Ganha na negociação", Em(5, 10)).Para(_funil, "Negociação", Em(7, 10)).Ganha(_funil, 50_000m, Em(31, 10));
        var naProposta = Venda(_funil, "Na proposta", Em(6, 10)).Para(_funil, "Proposta", Em(8, 10));
        var perdidaNoContato = Venda(_funil, "Perdida no contato", Em(7, 10)).Perdida(_funil, Em(8, 10));

        var conversao = ConversaoPorEtapa.Do(
            _funil,
            [parada, perdidaNaVisita, pulouAVisita, ganhaPassoAPasso, ganhaNaNegociacao, naProposta, perdidaNoContato],
            Outubro);

        Assert.Equal((Outubro, 7), (conversao.Periodo, conversao.Vendas));
        Assert.Equal(
            [
                "Contato: chegaram 7, avançaram 5, ganhas 2, perdidas 3, em andamento 2, taxa de ganho 40,0",
                "Visita: chegaram 5, avançaram 4, ganhas 2, perdidas 2, em andamento 1, taxa de ganho 50,0",
                "Proposta: chegaram 4, avançaram 2, ganhas 2, perdidas 1, em andamento 1, taxa de ganho 66,7",
                "Negociação: chegaram 2, avançaram 2, ganhas 2, perdidas 0, em andamento 0, taxa de ganho 100,0",
            ],
            conversao.Etapas.Select(Descrever));
    }

    [Fact]
    public void Entram_as_vendas_do_funil_que_nasceram_no_periodo_no_fuso_dele()
    {
        var setembro = Venda(_funil, "Setembro", Em(30, 9, 20));
        var noveDaNoite = Venda(_funil, "31/10, 22h em Brasília", new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        var novembro = Venda(_funil, "Novembro", Em(1, 11));
        var outroFunil = FunilSimples();
        var deOutroFunil = Venda(outroFunil, "De outro funil", Em(10, 10));

        var conversao = ConversaoPorEtapa.Do(_funil, [setembro, noveDaNoite, novembro, deOutroFunil], Outubro);

        Assert.Equal(1, conversao.Vendas);
        Assert.Equal(1, conversao.Etapas[0].Chegaram);
    }

    [Fact]
    public void Sem_venda_fechada_nao_ha_taxa_de_ganho()
    {
        var aberta = Venda(_funil, "Aberta", Em(1, 10)).Para(_funil, "Visita", Em(2, 10));

        var conversao = ConversaoPorEtapa.Do(_funil, [aberta], Outubro);

        Assert.All(conversao.Etapas, etapa => Assert.Null(etapa.TaxaDeGanho));
        Assert.Equal([1, 1, 0, 0], conversao.Etapas.Select(etapa => etapa.Chegaram));
    }

    [Fact]
    public void Sem_venda_no_periodo_ninguem_chega()
    {
        var conversao = ConversaoPorEtapa.Do(_funil, [], Outubro);

        Assert.Equal(0, conversao.Vendas);
        Assert.All(conversao.Etapas, etapa => Assert.Equal((0, 0, 0, 0, (decimal?)null), (etapa.Chegaram, etapa.Avancaram, etapa.Ganhas, etapa.Perdidas, etapa.TaxaDeGanho)));
    }

    [Fact]
    public void Segue_a_ordem_do_funil_hoje_so_com_as_etapas_em_uso()
    {
        var perdidaNaVisita = Venda(_funil, "Perdida na visita", Em(2, 10)).Para(_funil, "Visita", Em(5, 10)).Perdida(_funil, Em(9, 10));
        _funil.ArquivarEtapa(_funil.Abertas[1].Id);

        var conversao = ConversaoPorEtapa.Do(_funil, [perdidaNaVisita], Outubro);

        Assert.Equal(["Contato", "Proposta", "Negociação"], conversao.Etapas.Select(etapa => etapa.Etapa.Nome));
        Assert.Equal([1, 0, 0], conversao.Etapas.Select(etapa => etapa.Chegaram));
        Assert.Equal(0, conversao.Etapas[0].Avancaram);
    }

    [Fact]
    public void A_reaberta_conta_como_esta_hoje()
    {
        var reaberta = Venda(_funil, "Reaberta", Em(2, 10)).Para(_funil, "Proposta", Em(5, 10)).Perdida(_funil, Em(9, 10));
        reaberta.Reabrir(_funil, _funil.Abertas[1].Id, Em(15, 10));

        var conversao = ConversaoPorEtapa.Do(_funil, [reaberta], Outubro);

        Assert.Equal(
            [(1, 0, 0), (1, 0, 0), (1, 0, 0), (0, 0, 0)],
            conversao.Etapas.Select(etapa => (etapa.Chegaram, etapa.Perdidas, etapa.Ganhas)));
    }

    private static string Descrever(ConversaoNaEtapa etapa) =>
        $"{etapa.Etapa}: chegaram {etapa.Chegaram}, avançaram {etapa.Avancaram}, ganhas {etapa.Ganhas}, perdidas {etapa.Perdidas}, "
        + $"em andamento {etapa.EmAndamento}, taxa de ganho {etapa.TaxaDeGanho?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')}";
}
