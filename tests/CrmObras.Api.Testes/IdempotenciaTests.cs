using System.Net;
using CrmObras.Api.Recursos;
using CrmObras.Api.Seguranca;

namespace CrmObras.Api.Testes;

/*
 * Criar é seguro de repetir: a mesma chave de idempotência, com o mesmo
 * pedido, recebe a mesma resposta — e nada se cria duas vezes, nem com as
 * repetições chegando juntas (ADR-0013).
 */
public sealed class IdempotenciaTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Vendedor() => api.Cliente(_construtora, Papeis.Vendedor);

    private Task<HttpResponseMessage> Criar(object corpo, string? chave) =>
        Vendedor().Enviar(HttpMethod.Post, "/api/contas", corpo, chave: chave);

    private async Task<int> QuantasContas() =>
        (await Vendedor().LerComSucesso<List<ResumoResposta>>("/api/contas")).Count;

    [Fact]
    public async Task Criar_sem_chave_e_400()
    {
        var resposta = await Criar(new { nome = "Rota Sul" }, chave: null);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Idempotency-Key", problema.Detail, StringComparison.Ordinal);
        Assert.Equal(0, await QuantasContas());
    }

    [Fact]
    public async Task Chave_maior_que_255_caracteres_e_400()
    {
        var resposta = await Criar(new { nome = "Rota Sul" }, chave: new string('k', 256));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, await QuantasContas());
    }

    [Fact]
    public async Task A_repeticao_recebe_a_mesma_resposta_e_nada_se_cria_de_novo()
    {
        var chave = Guid.NewGuid().ToString();

        var primeira = await Criar(Exemplos.ContaDaRotaSul(), chave);
        var repetida = await Criar(Exemplos.ContaDaRotaSul(), chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (primeira.StatusCode, repetida.StatusCode));
        Assert.Equal(primeira.Headers.Location, repetida.Headers.Location);
        Assert.Equal(primeira.Headers.ETag, repetida.Headers.ETag);
        Assert.Equal(await primeira.Content.ReadAsStringAsync(Cancelamento), await repetida.Content.ReadAsStringAsync(Cancelamento));
        Assert.False(primeira.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", repetida.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task A_repeticao_devolve_a_resposta_original_mesmo_depois_de_alterar()
    {
        var chave = Guid.NewGuid().ToString();
        var primeira = await Criar(new { nome = "Rota Sul" }, chave);
        await Vendedor().Enviar(HttpMethod.Patch, primeira.Headers.Location!.ToString(), new { nome = "Logística Rota Sul" }, versao: 1).ComSucesso();

        var repetida = await Criar(new { nome = "Rota Sul" }, chave);

        Assert.Equal("\"1\"", repetida.Headers.ETag?.Tag);
        Assert.Contains("\"nome\":\"Rota Sul\"", await repetida.Content.ReadAsStringAsync(Cancelamento), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_mesma_chave_com_outro_pedido_e_422()
    {
        var chave = Guid.NewGuid().ToString();
        await Criar(new { nome = "Rota Sul" }, chave);

        var outro = await Criar(new { nome = "Construtora Alfa" }, chave);
        var problema = await outro.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, outro.StatusCode);
        Assert.EndsWith("#chave-reutilizada", problema.Type, StringComparison.Ordinal);
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task A_mesma_chave_noutro_caminho_e_outro_pedido()
    {
        var chave = Guid.NewGuid().ToString();
        await Criar(new { nome = "Rota Sul" }, chave);

        var lead = await Vendedor().Enviar(HttpMethod.Post, "/api/leads", new { nome = "Rota Sul", origem = "Site", email = "contato@rotasul.com.br" }, chave: chave);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, lead.StatusCode);
    }

    [Fact]
    public async Task Repeticoes_simultaneas_criam_uma_so()
    {
        var chave = Guid.NewGuid().ToString();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Criar(Exemplos.ContaDaRotaSul(), chave)));

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.Created, resposta.StatusCode));
        Assert.Single(respostas.Select(resposta => resposta.Headers.Location).Distinct());
        Assert.Equal(9, respostas.Count(resposta => resposta.Headers.Contains("Idempotent-Replayed")));
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task Pedido_recusado_nao_gasta_a_chave()
    {
        // A criação falhou, e a reserva da chave foi desfeita com ela: a
        // mesma chave serve para a tentativa corrigida.
        var chave = Guid.NewGuid().ToString();

        var recusado = await Criar(new { nome = "Rota Sul", documento = "11.222.333/0001-80" }, chave);
        var corrigido = await Criar(new { nome = "Rota Sul", documento = Exemplos.CnpjDaMatriz }, chave);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, HttpStatusCode.Created), (recusado.StatusCode, corrigido.StatusCode));
        Assert.False(corrigido.Headers.Contains("Idempotent-Replayed"));
    }

    [Fact]
    public async Task A_chave_de_uma_construtora_nao_vale_para_outra()
    {
        var chave = Guid.NewGuid().ToString();

        var daPrimeira = await Criar(new { nome = "Rota Sul" }, chave);
        var daSegunda = await api.Cliente(Guid.CreateVersion7(), Papeis.Vendedor)
            .Enviar(HttpMethod.Post, "/api/contas", new { nome = "Rota Sul" }, chave: chave);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (daPrimeira.StatusCode, daSegunda.StatusCode));
        Assert.NotEqual(daPrimeira.Headers.Location, daSegunda.Headers.Location);
    }
}
