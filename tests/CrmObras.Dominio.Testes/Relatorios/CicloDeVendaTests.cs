using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Relatorios;
using static CrmObras.Dominio.Testes.Relatorios.RelatoriosDeTeste;

namespace CrmObras.Dominio.Testes.Relatorios;

public sealed class CicloDeVendaTests
{
    private static readonly Periodo Outubro = Periodo.Do(Mes.De(2026, 10), Brasilia);

    private readonly Funil _funil = FunilSimples();

    [Fact]
    public void Mede_o_tempo_da_abertura_ao_ganho_e_em_cada_etapa()
    {
        var passoAPasso = Venda(_funil, "Passo a passo", Em(1, 10))
            .Para(_funil, "Visita", Em(5, 10))
            .Para(_funil, "Proposta", Em(12, 10))
            .Ganha(_funil, 100_000m, Em(21, 10));
        var direto = Venda(_funil, "Direto para a negociação", Em(1, 10)).Para(_funil, "Negociação", Em(3, 10)).Ganha(_funil, 50_000m, Em(31, 10));
        var deSetembro = Venda(_funil, "Aberta em setembro", Em(1, 9)).Para(_funil, "Proposta", Em(1, 10)).Ganha(_funil, 80_000m, Em(11, 10));
        var obraPublica = Venda(_funil, "Obra pública", Em(1, 1)).Para(_funil, "Proposta", Em(1, 10)).Ganha(_funil, 900_000m, Em(15, 10));
        var ganhaEmSetembro = Venda(_funil, "Ganha em setembro", Em(1, 8)).Ganha(_funil, 10_000m, Em(30, 9));
        var perdida = Venda(_funil, "Perdida", Em(1, 10)).Perdida(_funil, Em(20, 10));

        var ciclo = CicloDeVenda.Do(_funil, [direto, obraPublica, ganhaEmSetembro, passoAPasso, perdida, deSetembro], Outubro);

        // Ciclos de 20, 30, 40 e 287 dias: a obra pública puxa a média, e a mediana nem percebe.
        Assert.Equal((Outubro, 4), (ciclo.Periodo, ciclo.Ganhas));
        Assert.Equal(
            (TimeSpan.FromDays(35), TimeSpan.FromDays(94.25), TimeSpan.FromDays(20), TimeSpan.FromDays(287)),
            (ciclo.Mediana, ciclo.Media, ciclo.MaisCurto, ciclo.MaisLongo));
        Assert.Equal(
            [
                ("Contato", 4, (TimeSpan?)TimeSpan.FromDays(17)),
                ("Visita", 1, TimeSpan.FromDays(7)),
                ("Proposta", 3, TimeSpan.FromDays(10)),
                ("Negociação", 1, TimeSpan.FromDays(28)),
            ],
            ciclo.Etapas.Select(etapa => (etapa.Etapa.Nome, etapa.Vendas, etapa.Mediana)));
    }

    [Fact]
    public void Entram_as_ganhas_no_periodo_no_fuso_dele()
    {
        var anoNovo = Venda(_funil, "Ganha às 22h de 31/10 em Brasília", Em(1, 10)).Ganha(_funil, 1m, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        var novembro = Venda(_funil, "Ganha em novembro", Em(1, 10)).Ganha(_funil, 1m, Em(1, 11));
        var outroFunil = FunilSimples();
        var deOutroFunil = Venda(outroFunil, "De outro funil", Em(1, 10)).Ganha(outroFunil, 1m, Em(10, 10));

        var ciclo = CicloDeVenda.Do(_funil, [anoNovo, novembro, deOutroFunil], Outubro);

        Assert.Equal((1, (TimeSpan?)TimeSpan.FromHours(30 * 24 + 13)), (ciclo.Ganhas, ciclo.Mediana));
    }

    [Fact]
    public void Sem_venda_ganha_nao_ha_ciclo()
    {
        var ciclo = CicloDeVenda.Do(_funil, [Venda(_funil, "Aberta", Em(1, 10))], Outubro);

        Assert.Equal(
            (0, (TimeSpan?)null, (TimeSpan?)null, (TimeSpan?)null, (TimeSpan?)null),
            (ciclo.Ganhas, ciclo.Mediana, ciclo.Media, ciclo.MaisCurto, ciclo.MaisLongo));
        Assert.All(ciclo.Etapas, etapa => Assert.Equal((0, (TimeSpan?)null), (etapa.Vendas, etapa.Mediana)));
    }

    [Fact]
    public void A_mediana_de_uma_quantidade_impar_e_a_do_meio()
    {
        int[] ciclos = [10, 50, 20];
        var vendas = ciclos.Select(dias => Venda(_funil, $"{dias} dias", Em(1, 9)).Ganha(_funil, 1m, Em(1, 9).AddDays(dias)));

        var ciclo = CicloDeVenda.Do(_funil, vendas, Periodo.De(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31), Brasilia));

        Assert.Equal(TimeSpan.FromDays(20), ciclo.Mediana);
    }

    [Fact]
    public void O_tempo_perdida_antes_de_reabrir_nao_e_de_nenhuma_etapa_em_uso()
    {
        var reaberta = Venda(_funil, "Reaberta", Em(1, 10)).Perdida(_funil, Em(5, 10));
        reaberta.Reabrir(_funil, _funil.Primeira.Id, Em(15, 10));
        reaberta.Ganhar(_funil, Dinheiro.Reais(1m), Em(20, 10));

        var ciclo = CicloDeVenda.Do(_funil, [reaberta], Outubro);

        Assert.Equal(TimeSpan.FromDays(19), ciclo.Mediana);
        Assert.Equal((1, (TimeSpan?)TimeSpan.FromDays(4 + 5)), (ciclo.Etapas[0].Vendas, ciclo.Etapas[0].Mediana));
    }
}
