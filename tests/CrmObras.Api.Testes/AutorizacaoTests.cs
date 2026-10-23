using System.Net;
using CrmObras.Api.Seguranca;

namespace CrmObras.Api.Testes;

/*
 * Quem pode o quê (ADR-0014). Ler, qualquer pessoa da construtora. Cuidar de
 * contas e leads, quem vende: vendedor, gerente ou diretor. Configurar os
 * funis e remover o que foi cadastrado errado, a gerência: gerente ou
 * diretor. Quem não pode recebe 403, mesmo sendo da construtora.
 */
public sealed class AutorizacaoTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Gerente => api.Cliente(_construtora, Papeis.Gerente);

    private async Task<(string Conta, string Lead, string Funil)> UmDeCada() =>
    (
        (await Gerente.Criar("/api/contas", Exemplos.ContaDaRotaSul()).ComSucesso()).Headers.Location!.ToString(),
        (await Gerente.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString(),
        (await Gerente.Criar("/api/funis/padrao", null).ComSucesso()).Headers.Location!.ToString()
    );

    [Fact]
    public async Task Sem_papel_nao_se_cria_nem_se_altera_nada()
    {
        var (conta, lead, funil) = await UmDeCada();
        var semPapel = api.Cliente(_construtora);

        var respostas = new[]
        {
            await semPapel.Criar("/api/contas", new { nome = "Outra" }),
            await semPapel.Enviar(HttpMethod.Patch, conta, new { nome = "Outro nome" }, versao: 1),
            await semPapel.Enviar(HttpMethod.Post, $"{conta}/contatos", new { nome = "Rita", email = "rita@rotasul.com.br" }, versao: 1),
            await semPapel.Criar("/api/leads", Exemplos.LeadDaMarina()),
            await semPapel.Enviar(HttpMethod.Put, $"{lead}/avaliacao", Exemplos.AvaliacaoQueQualifica, versao: 1),
            await semPapel.Enviar(HttpMethod.Post, $"{lead}/conversao", new { }, versao: 1),
            await semPapel.Enviar(HttpMethod.Patch, funil, new { nome = "Outro" }, versao: 1),
        };

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode));
    }

    [Fact]
    public async Task O_vendedor_cuida_de_contas_e_leads_mas_nao_configura_funil_nem_remove()
    {
        var (conta, lead, funil) = await UmDeCada();
        var vendedor = api.Cliente(_construtora, Papeis.Vendedor);

        var podem = new[]
        {
            await vendedor.Criar("/api/contas", new { nome = "Construtora Alfa" }),
            await vendedor.Enviar(HttpMethod.Patch, conta, new { nome = "Rota Sul" }, versao: 1),
            await vendedor.Criar("/api/leads", Exemplos.LeadDaMarina(documento: null)),
            await vendedor.Enviar(HttpMethod.Put, $"{lead}/avaliacao", Exemplos.AvaliacaoQueQualifica, versao: 1),
        };

        var naoPodem = new[]
        {
            await vendedor.Criar("/api/funis/padrao", null),
            await vendedor.Criar("/api/funis", Exemplos.FunilDeObraPublica),
            await vendedor.Enviar(HttpMethod.Patch, funil, new { nome = "Outro" }, versao: 1),
            await vendedor.Enviar(HttpMethod.Delete, funil, versao: 1),
            await vendedor.Enviar(HttpMethod.Delete, conta, versao: 2),
            await vendedor.Enviar(HttpMethod.Delete, lead, versao: 2),
        };

        Assert.All(podem, resposta => Assert.True(resposta.IsSuccessStatusCode, $"{resposta.RequestMessage}: {resposta.StatusCode}"));
        Assert.All(naoPodem, resposta => Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode));
    }

    [Theory]
    [InlineData(Papeis.Gerente)]
    [InlineData(Papeis.Diretor)]
    public async Task A_gerencia_configura_funis_e_remove(string papel)
    {
        var (conta, lead, funil) = await UmDeCada();
        var gerencia = api.Cliente(_construtora, papel);

        var respostas = new[]
        {
            await gerencia.Criar("/api/funis", Exemplos.FunilDeObraPublica),
            await gerencia.Enviar(HttpMethod.Patch, funil, new { nome = "Vendas de galpões" }, versao: 1),
            await gerencia.Enviar(HttpMethod.Delete, funil, versao: 2),
            await gerencia.Enviar(HttpMethod.Delete, conta, versao: 1),
            await gerencia.Enviar(HttpMethod.Delete, lead, versao: 1),
        };

        Assert.All(respostas, resposta => Assert.True(resposta.IsSuccessStatusCode, $"{resposta.RequestMessage}: {resposta.StatusCode}"));
    }

    [Theory]
    [InlineData(Papeis.Gerente)]
    [InlineData(Papeis.Diretor)]
    public async Task A_gerencia_tambem_vende(string papel)
    {
        var gerencia = api.Cliente(_construtora, papel);

        var conta = await gerencia.Criar("/api/contas", Exemplos.ContaDaRotaSul());
        var lead = await gerencia.Criar("/api/leads", Exemplos.LeadDaMarina());
        var avaliado = await gerencia.Enviar(HttpMethod.Put, $"{lead.Headers.Location}/avaliacao", Exemplos.AvaliacaoQueQualifica, versao: 1);

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.OK),
            (conta.StatusCode, lead.StatusCode, avaliado.StatusCode));
    }

    [Fact]
    public async Task Qualquer_pessoa_da_construtora_le_tudo()
    {
        var (conta, lead, funil) = await UmDeCada();
        var semPapel = api.Cliente(_construtora);

        var leituras = new[]
        {
            await semPapel.GetAsync("/api/contas", Cancelamento),
            await semPapel.GetAsync(conta, Cancelamento),
            await semPapel.GetAsync("/api/leads?situacao=Novo", Cancelamento),
            await semPapel.GetAsync(lead, Cancelamento),
            await semPapel.GetAsync($"{lead}/duplicados", Cancelamento),
            await semPapel.GetAsync("/api/funis", Cancelamento),
            await semPapel.GetAsync(funil, Cancelamento),
        };

        Assert.All(leituras, resposta => Assert.Equal(HttpStatusCode.OK, resposta.StatusCode));
    }
}
