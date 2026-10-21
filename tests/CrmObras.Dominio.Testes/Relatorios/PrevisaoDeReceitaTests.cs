using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Relatorios;
using static CrmObras.Dominio.Testes.Relatorios.RelatoriosDeTeste;

namespace CrmObras.Dominio.Testes.Relatorios;

public sealed class PrevisaoDeReceitaTests
{
    /// <summary>Quarta, 21/10/2026, às 14h.</summary>
    private static readonly DateTimeOffset Agora = Em(21, 10, 14);

    private static readonly Mes Outubro = Mes.De(2026, 10);

    private readonly Funil _funil = FunilSimples();

    [Fact]
    public void Soma_o_ganho_e_pondera_o_que_esta_em_aberto_mes_a_mes()
    {
        var galpao = Venda(_funil, "Galpão", Em(1, 9), 1_000_000m, new DateOnly(2026, 11, 15)).Para(_funil, "Proposta", Em(10, 10));
        var muro = Venda(_funil, "Muro", Em(1, 10), 200_000m, new DateOnly(2026, 11, 30));
        var portaria = Venda(_funil, "Portaria", Em(1, 9), 400_000m, new DateOnly(2026, 10, 21)).Para(_funil, "Negociação", Em(15, 10));
        var reforma = Venda(_funil, "Reforma", Em(1, 9)).Ganha(_funil, 250_000m, Em(18, 10));
        var guarita = Venda(_funil, "Guarita", Em(1, 9)).Ganha(_funil, 50_000m, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        var anexo = Venda(_funil, "Anexo", Em(1, 8)).Ganha(_funil, 90_000m, Em(1, 9));
        var deposito = Venda(_funil, "Depósito", Em(1, 9), 300_000m, new DateOnly(2026, 12, 20)).Perdida(_funil, Em(5, 10));
        var escola = Venda(_funil, "Escola", Em(1, 10), 100_000m, new DateOnly(2027, 1, 15));

        var previsao = PrevisaoDeReceita.Do([_funil], [galpao, muro, portaria, reforma, guarita, anexo, deposito, escola], Outubro, 3, Agora);

        Assert.Equal(
            [
                "10/2026: ganho R$ 300.000,00, em aberto R$ 400.000,00, ponderado R$ 320.000,00, previsto R$ 620.000,00",
                "11/2026: ganho R$ 0,00, em aberto R$ 1.200.000,00, ponderado R$ 520.000,00, previsto R$ 520.000,00",
                "12/2026: ganho R$ 0,00, em aberto R$ 0,00, ponderado R$ 0,00, previsto R$ 0,00",
            ],
            previsao.Meses.Select(mes => $"{mes.Mes}: ganho {mes.Ganho}, em aberto {mes.EmAberto}, ponderado {mes.Ponderado}, previsto {mes.Previsto}"));
        Assert.Equal([portaria], previsao.Meses[0].Abertas);
        Assert.Equal([galpao, muro], previsao.Meses[1].Abertas);
        Assert.Equal(
            (Dinheiro.Reais(300_000m), Dinheiro.Reais(840_000m), Dinheiro.Reais(1_140_000m)),
            (previsao.Ganho, previsao.Ponderado, previsao.Previsto));
    }

    [Fact]
    public void O_ganho_conta_no_mes_do_fuso_de_quem_pede()
    {
        // 1º/11, 1h em Greenwich, é 31/10, 22h em Brasília.
        var guarita = Venda(_funil, "Guarita", Em(1, 9)).Ganha(_funil, 50_000m, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));

        var emBrasilia = PrevisaoDeReceita.Do([_funil], [guarita], Outubro, 2, Agora);
        var emGreenwich = PrevisaoDeReceita.Do([_funil], [guarita], Outubro, 2, Agora.ToOffset(TimeSpan.Zero));

        Assert.Equal((Dinheiro.Reais(50_000m), Dinheiro.Zero), (emBrasilia.Meses[0].Ganho, emBrasilia.Meses[1].Ganho));
        Assert.Equal((Dinheiro.Zero, Dinheiro.Reais(50_000m)), (emGreenwich.Meses[0].Ganho, emGreenwich.Meses[1].Ganho));
    }

    [Fact]
    public void O_que_a_previsao_nao_pode_prever_fica_em_listas_a_parte()
    {
        var hoje = Venda(_funil, "Hoje", Em(1, 10), 100_000m, new DateOnly(2026, 10, 21));
        var ontem = Venda(_funil, "Ontem", Em(1, 10), 100_000m, new DateOnly(2026, 10, 20));
        var setembro = Venda(_funil, "Setembro", Em(1, 9), 100_000m, new DateOnly(2026, 9, 30));
        var semValor = Venda(_funil, "Sem valor", Em(1, 10), previsao: new DateOnly(2026, 11, 10));
        var semNada = Venda(_funil, "Sem nada", Em(1, 10));
        var semData = Venda(_funil, "Sem data", Em(1, 10), 100_000m);

        var previsao = PrevisaoDeReceita.Do([_funil], [hoje, ontem, setembro, semValor, semNada, semData], Outubro, 2, Agora);

        Assert.Equal([hoje], previsao.Meses[0].Abertas);
        Assert.Empty(previsao.Meses[1].Abertas);
        Assert.Equal([setembro, ontem], previsao.Vencidas);
        Assert.Equal([semValor, semNada], previsao.SemValor);
        Assert.Equal([semData], previsao.SemPrevisao);
    }

    [Fact]
    public void Fechada_nao_e_vencida_nem_esta_sem_valor()
    {
        var perdida = Venda(_funil, "Perdida", Em(1, 9), 100_000m, new DateOnly(2026, 9, 30)).Perdida(_funil, Em(1, 10));
        var ganha = Venda(_funil, "Ganha", Em(1, 9)).Ganha(_funil, 10_000m, Em(1, 10));

        var previsao = PrevisaoDeReceita.Do([_funil], [perdida, ganha], Outubro, 1, Agora);

        Assert.Equal((0, 0, 0), (previsao.Vencidas.Count, previsao.SemValor.Count, previsao.SemPrevisao.Count));
        Assert.Empty(previsao.Meses[0].Abertas);
    }

    [Fact]
    public void O_ponderado_e_arredondado_no_mes_e_nao_venda_a_venda()
    {
        // 10% de R$ 0,05 é meio centavo: cada um, sozinho, iria a zero; os dois somam um centavo.
        var uma = Venda(_funil, "Uma", Em(1, 10), 0.05m, new DateOnly(2026, 11, 10));
        var outra = Venda(_funil, "Outra", Em(1, 10), 0.05m, new DateOnly(2026, 11, 20));
        var sozinha = Venda(_funil, "Sozinha", Em(1, 10), 0.05m, new DateOnly(2026, 12, 10));

        var previsao = PrevisaoDeReceita.Do([_funil], [uma, outra, sozinha], Mes.De(2026, 11), 2, Agora);

        // Sozinho no mês, o meio centavo vai para o par: zero.
        Assert.Equal([Dinheiro.Reais(0.01m), Dinheiro.Zero], previsao.Meses.Select(mes => mes.Ponderado));
    }

    [Fact]
    public void A_venda_em_etapa_arquivada_conta_pela_probabilidade_dela()
    {
        var galpao = Venda(_funil, "Galpão", Em(1, 10), 1_000_000m, new DateOnly(2026, 11, 15)).Para(_funil, "Visita", Em(2, 10));
        _funil.ArquivarEtapa(_funil.Etapa(galpao.Etapa).Id);

        var novembro = PrevisaoDeReceita.Do([_funil], [galpao], Mes.De(2026, 11), 1, Agora).Meses[0];

        Assert.Equal(Dinheiro.Reais(250_000m), novembro.Ponderado);
    }

    [Fact]
    public void Primeiro_mes_no_passado_so_tem_ganho()
    {
        var anexo = Venda(_funil, "Anexo", Em(1, 8)).Ganha(_funil, 90_000m, Em(1, 9));
        var reforma = Venda(_funil, "Reforma", Em(1, 8)).Ganha(_funil, 30_000m, Em(5, 10));
        var galpao = Venda(_funil, "Galpão", Em(1, 10), 1_000_000m, new DateOnly(2026, 10, 30));

        var previsao = PrevisaoDeReceita.Do([_funil], [anexo, reforma, galpao], Mes.De(2026, 9), 2, Agora);

        Assert.Equal(
            [(Mes.De(2026, 9), Dinheiro.Reais(90_000m), Dinheiro.Zero), (Outubro, Dinheiro.Reais(30_000m), Dinheiro.Reais(100_000m))],
            previsao.Meses.Select(mes => (mes.Mes, mes.Ganho, mes.Ponderado)));
        Assert.Equal((Dinheiro.Reais(120_000m), Dinheiro.Reais(220_000m)), (previsao.Ganho, previsao.Previsto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Vai_de_1_a_24_meses(int meses)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => PrevisaoDeReceita.Do([_funil], [], Outubro, meses, Agora));

        Assert.Equal($"A previsão vai de 1 a 24 meses: {meses} não serve.", erro.Message);
        Assert.Equal(24, PrevisaoDeReceita.Do([_funil], [], Outubro, 24, Agora).Meses.Count);
        Assert.Equal(Mes.De(2028, 9), PrevisaoDeReceita.Do([_funil], [], Outubro, 24, Agora).Meses[^1].Mes);
    }

    [Fact]
    public void Precisa_do_funil_de_cada_venda_prevista()
    {
        var galpao = Venda(_funil, "Galpão", Em(1, 10), 1_000_000m, new DateOnly(2026, 11, 15));

        var erro = Assert.Throws<ArgumentException>(() => PrevisaoDeReceita.Do([FunilSimples()], [galpao], Outubro, 2, Agora));

        Assert.StartsWith("Falta o funil da oportunidade Galpão.", erro.Message, StringComparison.Ordinal);
    }
}
