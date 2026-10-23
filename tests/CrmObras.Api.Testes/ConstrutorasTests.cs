using System.Net;
using CrmObras.Api.Recursos;
using CrmObras.Api.Recursos.Leads;
using CrmObras.Api.Seguranca;

namespace CrmObras.Api.Testes;

/*
 * Duas construtoras na mesma API. A construtora vem do token; para a outra,
 * a conta não existe — 404, e não 403: "existe, mas não é sua" já seria
 * vazar (ADR-0011).
 */
public sealed class ConstrutorasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _alfa = Guid.CreateVersion7();

    private readonly Guid _beta = Guid.CreateVersion7();

    [Fact]
    public async Task O_que_uma_construtora_cria_nao_existe_para_a_outra()
    {
        var daAlfa = api.Cliente(_alfa, Papeis.Gerente);
        var daBeta = api.Cliente(_beta, Papeis.Gerente);
        var conta = (await daAlfa.Criar("/api/contas", Exemplos.ContaDaRotaSul()).ComSucesso()).Headers.Location!.ToString();
        var funil = (await daAlfa.Criar("/api/funis/padrao", null).ComSucesso()).Headers.Location!.ToString();

        var respostas = new[]
        {
            await daBeta.GetAsync(conta, Cancelamento),
            await daBeta.Enviar(HttpMethod.Patch, conta, new { nome = "Invadida" }, versao: 1),
            await daBeta.Enviar(HttpMethod.Delete, conta, versao: 1),
            await daBeta.GetAsync(funil, Cancelamento),
            await daBeta.Enviar(HttpMethod.Delete, funil, versao: 1),
        };

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode));
        Assert.Empty(await daBeta.LerComSucesso<List<ResumoResposta>>("/api/contas"));
        Assert.Empty(await daBeta.LerComSucesso<List<ResumoResposta>>("/api/funis"));
        Assert.Equal(HttpStatusCode.OK, (await daAlfa.GetAsync(conta, Cancelamento)).StatusCode);
    }

    [Fact]
    public async Task A_mensagem_para_a_outra_construtora_e_a_mesma_de_um_id_que_nunca_existiu()
    {
        var conta = (await api.Cliente(_alfa, Papeis.Vendedor).Criar("/api/contas", new { nome = "Rota Sul" }).ComSucesso()).Headers.Location!.ToString();
        var inventada = $"/api/contas/{Guid.CreateVersion7()}";
        var daBeta = api.Cliente(_beta);

        var deOutraConstrutora = await (await daBeta.GetAsync(conta, Cancelamento)).Problema();
        var queNuncaExistiu = await (await daBeta.GetAsync(inventada, Cancelamento)).Problema();

        Assert.Equal($"Não existe a conta {conta.Split('/')[^1]}.", deOutraConstrutora.Detail);
        Assert.Equal($"Não existe a conta {inventada.Split('/')[^1]}.", queNuncaExistiu.Detail);
        Assert.Equal(queNuncaExistiu.Title, deOutraConstrutora.Title);
    }

    [Fact]
    public async Task O_lead_nao_acha_duplicado_nem_vira_cliente_na_conta_de_outra_construtora()
    {
        var contaDaAlfa = await api.Cliente(_alfa, Papeis.Vendedor).Criar("/api/contas", Exemplos.ContaDaRotaSul(Exemplos.CnpjDaFilial)).IdCriado();
        var daBeta = api.Cliente(_beta, Papeis.Vendedor);
        var lead = (await daBeta.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();
        await daBeta.Enviar(HttpMethod.Put, $"{lead}/avaliacao", Exemplos.AvaliacaoQueQualifica, versao: 1).ComSucesso();
        await daBeta.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 2).ComSucesso();

        var duplicados = await daBeta.LerComSucesso<List<PossivelDuplicadoResposta>>($"{lead}/duplicados");
        var naContaDaAlfa = await daBeta.Enviar(HttpMethod.Post, $"{lead}/conversao", new { conta = contaDaAlfa }, versao: 3);
        var emContaNova = await daBeta.Enviar(HttpMethod.Post, $"{lead}/conversao", new { }, versao: 3);

        Assert.Empty(duplicados);
        Assert.Equal(HttpStatusCode.NotFound, naContaDaAlfa.StatusCode);
        Assert.Equal(HttpStatusCode.OK, emContaNova.StatusCode);
    }
}
