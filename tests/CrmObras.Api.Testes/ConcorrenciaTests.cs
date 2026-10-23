using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Seguranca;

namespace CrmObras.Api.Testes;

/*
 * A versão que o banco confere (ADR-0012), pelo HTTP: sai na ETag de toda
 * leitura e volta no If-Match de toda alteração. Sem If-Match, 428; com uma
 * versão que já não é a atual, 412 — e o corpo do problema diz qual é a
 * atual (ADR-0014).
 */
public sealed class ConcorrenciaTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Vendedor => api.Cliente(_construtora, Papeis.Vendedor);

    private async Task<string> UmaConta() =>
        (await Vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul()).ComSucesso()).Headers.Location!.ToString();

    private Task<HttpResponseMessage> Renomear(string conta, string nome, int? versao) =>
        Vendedor.Enviar(HttpMethod.Patch, conta, new { nome }, versao);

    [Fact]
    public async Task A_versao_sai_na_ETag_de_toda_leitura_e_sobe_a_cada_alteracao()
    {
        var criada = await Vendedor.Criar("/api/contas", new { nome = "Rota Sul" });
        var conta = criada.Headers.Location!.ToString();
        var lida = await Vendedor.GetAsync(conta, Cancelamento);
        var renomeada = await Renomear(conta, "Logística Rota Sul", versao: 1);

        Assert.Equal("\"1\"", criada.Headers.ETag?.Tag);
        Assert.False(criada.Headers.ETag?.IsWeak);
        Assert.Equal("\"1\"", lida.Headers.ETag?.Tag);
        Assert.Equal("\"2\"", renomeada.Headers.ETag?.Tag);
        Assert.Equal("Logística Rota Sul", (await renomeada.Ler<ContaResposta>()).Nome);
    }

    [Fact]
    public async Task Alteracao_sem_If_Match_e_428()
    {
        var conta = await UmaConta();

        var resposta = await Renomear(conta, "Rota Sul", versao: null);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.PreconditionRequired, resposta.StatusCode);
        Assert.EndsWith("#versao-ausente", problema.Type, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("*", HttpStatusCode.PreconditionRequired)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    [InlineData("W/\"1\"", HttpStatusCode.BadRequest)]
    [InlineData("\"1\", \"2\"", HttpStatusCode.BadRequest)]
    [InlineData("\"zero\"", HttpStatusCode.BadRequest)]
    [InlineData("\"0\"", HttpStatusCode.BadRequest)]
    public async Task If_Match_que_nao_e_uma_versao_so_e_recusado(string ifMatch, HttpStatusCode esperado)
    {
        var conta = await UmaConta();

        using var pedido = new HttpRequestMessage(HttpMethod.Patch, conta) { Content = JsonContent.Create(new { nome = "Rota Sul" }) };
        pedido.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        var resposta = await Vendedor.SendAsync(pedido, Cancelamento);

        Assert.Equal(esperado, resposta.StatusCode);
    }

    [Fact]
    public async Task Versao_que_ja_nao_e_a_atual_e_412_com_a_versao_atual()
    {
        var conta = await UmaConta();
        await Renomear(conta, "Rota Sul", versao: 1).ComSucesso();

        var atrasado = await Renomear(conta, "Rota Sul Transportes", versao: 1);
        var problema = await atrasado.Problema();

        Assert.Equal(HttpStatusCode.PreconditionFailed, atrasado.StatusCode);
        Assert.Equal("2", problema.Extensions["versaoAtual"]?.ToString());
        Assert.Null(atrasado.Headers.ETag);
        Assert.True(atrasado.Headers.CacheControl?.NoStore);
        Assert.Contains("mudou desde a versão 1: está na versão 2", problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versao_velha_e_412_antes_de_qualquer_outro_erro()
    {
        // Na versão 1 o contato existia; na 2, não. Quem leu a 1 fica sabendo
        // que a conta mudou — e não recebe um 404 sobre um estado que nunca
        // viu.
        var conta = await UmaConta();
        var contato = (await Vendedor.LerComSucesso<ContaResposta>(conta)).Contatos.Single().Id;
        await Vendedor.Enviar(HttpMethod.Delete, $"{conta}/contatos/{contato}", versao: 1).ComSucesso();

        var resposta = await Vendedor.Enviar(HttpMethod.Put, $"{conta}/contatos/{contato}", new { nome = "Otávio", email = "otavio@rotasul.com.br" }, versao: 1);

        Assert.Equal(HttpStatusCode.PreconditionFailed, resposta.StatusCode);
    }

    [Fact]
    public async Task Remover_com_versao_velha_e_412_e_a_conta_fica()
    {
        var conta = await UmaConta();
        await Renomear(conta, "Rota Sul", versao: 1).ComSucesso();

        var remover = await api.Cliente(_construtora, Papeis.Gerente).Enviar(HttpMethod.Delete, conta, versao: 1);

        Assert.Equal(HttpStatusCode.PreconditionFailed, remover.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Vendedor.GetAsync(conta, Cancelamento)).StatusCode);
    }

    [Fact]
    public async Task De_alteracoes_simultaneas_sobre_a_mesma_versao_so_uma_passa()
    {
        var conta = await UmaConta();

        var respostas = await Task.WhenAll(Enumerable.Range(1, 10).Select(n => Renomear(conta, $"Rota Sul {n}", versao: 1)));

        Assert.Equal(1, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.PreconditionFailed));
    }

    [Fact]
    public async Task Quem_ja_tem_a_versao_atual_recebe_304()
    {
        var conta = await UmaConta();

        using var condicional = new HttpRequestMessage(HttpMethod.Get, conta);
        condicional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"1\""));
        var naoMudou = await Vendedor.SendAsync(condicional, Cancelamento);

        await Renomear(conta, "Rota Sul", versao: 1).ComSucesso();

        using var depois = new HttpRequestMessage(HttpMethod.Get, conta);
        depois.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"1\""));
        var mudou = await Vendedor.SendAsync(depois, Cancelamento);

        Assert.Equal(HttpStatusCode.NotModified, naoMudou.StatusCode);
        Assert.Empty(await naoMudou.Content.ReadAsByteArrayAsync(Cancelamento));
        Assert.Equal((HttpStatusCode.OK, "\"2\""), (mudou.StatusCode, mudou.Headers.ETag?.Tag));
    }
}
