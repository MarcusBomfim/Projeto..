using System.Net;
using CrmObras.Api.Recursos.Relatorios;

namespace CrmObras.Api.Testes;

/*
 * Os relatórios pela API: o que se pergunta na consulta, e o que acontece
 * quando a pergunta vem errada. Os números de um trimestre inteiro estão no
 * exemplo do README pela API.
 */
public sealed class RelatoriosTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly CenarioDeVenda _cenario = new(api);

    [Fact]
    public async Task A_previsao_comeca_no_mes_de_hoje_e_vai_por_tres_meses()
    {
        _cenario.Relogio(CenarioDeVenda.Segunda);
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        await _cenario.Carlos.Criar("/api/oportunidades", new { conta = filial, funil = funil.Id, titulo = "Portaria", valorEstimado = 150_000m, previsaoDeFechamento = "2026-11-20" })
            .ComSucesso();
        var semValor = await _cenario.Venda(filial, funil, "Galpão");

        var previsao = await _cenario.Leitor.LerComSucesso<PrevisaoResposta>("/api/relatorios/previsao");

        Assert.Equal(["2026-10", "2026-11", "2026-12"], previsao.Meses.Select(mes => mes.Mes));
        Assert.Equal((150_000m, 15_000m), (previsao.Meses[1].EmAberto, previsao.Meses[1].Ponderado));
        Assert.Equal([semValor.Id], previsao.SemValor.Select(venda => venda.Id));
    }

    [Theory]
    [InlineData("/api/relatorios/previsao?primeiro=2026-13", HttpStatusCode.BadRequest)]
    [InlineData("/api/relatorios/previsao?primeiro=10/2026", HttpStatusCode.BadRequest)]
    [InlineData("/api/relatorios/previsao?meses=dois", HttpStatusCode.BadRequest)]
    [InlineData("/api/relatorios/previsao?meses=25", HttpStatusCode.UnprocessableEntity)]
    public async Task A_previsao_pergunta_o_mes_e_quantos_meses(string caminho, HttpStatusCode esperado)
    {
        var resposta = await _cenario.Leitor.GetAsync(caminho, Cancelamento);

        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Theory]
    [InlineData("conversao")]
    [InlineData("ciclo")]
    [InlineData("perdas")]
    public async Task Os_relatorios_do_funil_pedem_o_funil_e_os_dias_por_extenso(string relatorio)
    {
        var funil = (await _cenario.FunilPadrao()).Id;
        var caminho = $"/api/relatorios/{relatorio}";

        var semFunil = await _cenario.Leitor.GetAsync($"{caminho}?de=2026-10-01&ate=2026-12-31", Cancelamento);
        var diaAmbiguo = await _cenario.Leitor.GetAsync($"{caminho}?funil={funil}&de=01/10/2026&ate=2026-12-31", Cancelamento);
        var invertido = await _cenario.Leitor.GetAsync($"{caminho}?funil={funil}&de=2026-12-31&ate=2026-10-01", Cancelamento);
        var deOutroFunil = await _cenario.Leitor.GetAsync($"{caminho}?funil={Guid.CreateVersion7()}&de=2026-10-01&ate=2026-12-31", Cancelamento);
        var certo = await _cenario.Leitor.GetAsync($"{caminho}?funil={funil}&de=2026-10-01&ate=2026-12-31", Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, semFunil.StatusCode);
        Assert.Equal("O parâmetro de é um dia, como 2026-10-19; veio \"01/10/2026\".", (await diaAmbiguo.Problema()).Detail);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invertido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deOutroFunil.StatusCode);
        Assert.Equal(HttpStatusCode.OK, certo.StatusCode);
    }

    [Fact]
    public async Task Os_dias_do_periodo_sao_os_do_fuso_da_construtora()
    {
        // Ganha às 22h de 31/12 em Brasília: em Greenwich já é 1º de janeiro, e para a construtora ainda é dezembro.
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        _cenario.Relogio(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(-3)));
        var venda = (await _cenario.Venda(filial, funil)).Id;
        _cenario.Relogio(new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.FromHours(-3)));
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/ganho", new { valorFechado = 100_000m }, versao: 1).ComSucesso();

        var dezembro = await _cenario.Leitor.LerComSucesso<CicloDeVendaResposta>($"/api/relatorios/ciclo?funil={funil.Id}&de=2026-12-01&ate=2026-12-31");
        var janeiro = await _cenario.Leitor.LerComSucesso<CicloDeVendaResposta>($"/api/relatorios/ciclo?funil={funil.Id}&de=2027-01-01&ate=2027-01-31");

        Assert.Equal((1, 0), (dezembro.Ganhas, janeiro.Ganhas));
    }

    [Fact]
    public async Task A_linha_do_tempo_de_conta_que_nao_existe_e_404()
    {
        var resposta = await _cenario.Leitor.GetAsync($"/api/contas/{Guid.CreateVersion7()}/linha-do-tempo", Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
