using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class QuadroDoFunilTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Uma_coluna_por_etapa_aberta_as_vazias_inclusive()
    {
        var quadro = QuadroDoFunil.Do(_funil, [], Segunda);

        Assert.Equal(_funil.Abertas, quadro.Colunas.Select(coluna => coluna.Etapa));
        Assert.All(quadro.Colunas, coluna => Assert.Empty(coluna.Oportunidades));
        Assert.Equal((Dinheiro.Zero, Dinheiro.Zero), (quadro.Total, quadro.Ponderado));
    }

    [Fact]
    public void Soma_cada_coluna_e_pondera_pela_probabilidade()
    {
        var galpao = Venda("Galpão", 2_400_000m, "Proposta enviada", Segunda.AddDays(1));
        var escritorio = Venda("Escritório", 600_000m, "Proposta enviada", Segunda.AddDays(2));
        var portaria = Venda("Portaria", 150_000m, "Negociação", Segunda.AddDays(3));

        var quadro = QuadroDoFunil.Do(_funil, [galpao, escritorio, portaria], Segunda.AddDays(4));
        var proposta = quadro.Colunas[3];
        var negociacao = quadro.Colunas[4];

        Assert.Equal([galpao, escritorio], proposta.Oportunidades);
        Assert.Equal((Dinheiro.Reais(3_000_000m), Dinheiro.Reais(1_800_000m)), (proposta.Total, proposta.Ponderado));
        Assert.Equal([portaria], negociacao.Oportunidades);
        Assert.Equal((Dinheiro.Reais(150_000m), Dinheiro.Reais(120_000m)), (negociacao.Total, negociacao.Ponderado));
        Assert.Equal((Dinheiro.Reais(3_150_000m), Dinheiro.Reais(1_920_000m)), (quadro.Total, quadro.Ponderado));
    }

    [Fact]
    public void O_ponderado_e_arredondado_ao_centavo_em_cada_coluna()
    {
        var reparo = Venda("Reparo", 1_000.05m, "Primeiro contato", Segunda);
        var muro = Venda("Muro", 1_000.15m, "Visita técnica", Segunda);

        var quadro = QuadroDoFunil.Do(_funil, [reparo, muro], Segunda);

        // 10% de 1.000,05 é 100,005: meio centavo, e o 0 é par. 20% de 1.000,15 é 200,03.
        Assert.Equal((Dinheiro.Reais(100.00m), Dinheiro.Reais(200.03m)), (quadro.Colunas[0].Ponderado, quadro.Colunas[1].Ponderado));
        Assert.Equal(Dinheiro.Reais(300.03m), quadro.Ponderado);
    }

    [Fact]
    public void Venda_sem_valor_conta_zero_e_aparece_como_sem_valor()
    {
        var semValor = DoGalpao(_funil, _rotaSul);
        var escritorio = Venda("Escritório", 600_000m, "Primeiro contato", Segunda);
        var portaria = Venda("Portaria", 150_000m, "Primeiro contato", Segunda);

        var coluna = QuadroDoFunil.Do(_funil, [semValor, escritorio, portaria], Segunda).Colunas[0];

        Assert.Equal((3, 1, Dinheiro.Reais(750_000m)), (coluna.Oportunidades.Count, coluna.SemValor, coluna.Total));
    }

    [Fact]
    public void Na_coluna_a_que_entrou_ha_mais_tempo_vem_primeiro()
    {
        var recente = Venda("Recente", 1m, "Visita técnica", Segunda.AddDays(5));
        var antiga = Venda("Antiga", 1m, "Visita técnica", Segunda.AddDays(1));
        var meio = Venda("Meio", 1m, "Visita técnica", Segunda.AddDays(3));

        var coluna = QuadroDoFunil.Do(_funil, [recente, antiga, meio], Segunda.AddDays(6)).Colunas[1];

        Assert.Equal(["Antiga", "Meio", "Recente"], coluna.Oportunidades.Select(oportunidade => oportunidade.Titulo));
    }

    [Fact]
    public void Mostra_as_paradas_de_cada_coluna()
    {
        var parada = Venda("Parada", 1m, "Proposta enviada", Segunda);
        var emDia = Venda("Em dia", 1m, "Proposta enviada", Segunda.AddDays(4));

        var coluna = QuadroDoFunil.Do(_funil, [parada, emDia], Segunda.AddDays(16)).Colunas[3];

        Assert.Equal([parada], coluna.Paradas);
        Assert.Equal(2, coluna.Oportunidades.Count);
    }

    [Fact]
    public void So_entram_as_vendas_abertas_do_funil()
    {
        var aberta = Venda("Aberta", 1_000m, "Negociação", Segunda);
        var ganha = Venda("Ganha", 2_000m, "Negociação", Segunda);
        ganha.Ganhar(_funil, Dinheiro.Reais(2_000m), Segunda.AddDays(1));
        var perdida = Venda("Perdida", 3_000m, "Negociação", Segunda);
        perdida.Perder(_funil, new Perda(MotivoDaPerda.Preco), Segunda.AddDays(1));
        var outroFunil = new Funil("Obras públicas", [new("Edital publicado", 5)]);
        var deOutroFunil = new Oportunidade(_rotaSul.Id, "Escola", outroFunil, "carlos.souza", Segunda);

        var quadro = QuadroDoFunil.Do(_funil, [aberta, ganha, perdida, deOutroFunil], Segunda.AddDays(2));

        Assert.Equal([aberta], quadro.Colunas.SelectMany(coluna => coluna.Oportunidades));
        Assert.Equal(Dinheiro.Reais(1_000m), quadro.Total);
    }

    [Fact]
    public void A_arquivada_com_venda_aparece_no_fim_e_a_vazia_some()
    {
        var naVisita = Venda("Na visita", 1m, "Visita técnica", Segunda);
        var visita = _funil.Chamada("Visita técnica");
        var orcamento = _funil.Chamada("Orçamento em elaboração");
        _funil.ArquivarEtapa(visita.Id);
        _funil.ArquivarEtapa(orcamento.Id);

        var quadro = QuadroDoFunil.Do(_funil, [naVisita], Segunda.AddDays(1));

        Assert.Equal(
            ["Primeiro contato", "Proposta enviada", "Negociação", "Visita técnica"],
            quadro.Colunas.Select(coluna => coluna.Etapa.Nome));
        Assert.Equal([naVisita], quadro.Colunas[^1].Oportunidades);
    }

    private Oportunidade Venda(string titulo, decimal valor, string etapa, DateTimeOffset entrouEm)
    {
        var oportunidade = Completa(_funil, _rotaSul);
        oportunidade.Renomear(titulo);
        oportunidade.Estimar(Dinheiro.Reais(valor));

        if (etapa != _funil.Primeira.Nome)
        {
            oportunidade.MoverPara(_funil, _funil.Chamada(etapa).Id, entrouEm);
        }

        return oportunidade;
    }
}
