using System.Net;
using CrmObras.Api.Recursos;
using CrmObras.Api.Recursos.Funis;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Funis;

namespace CrmObras.Api.Testes;

/*
 * Os funis pela API: o padrão pronto, o que a gerência monta, e as etapas
 * que entram, mudam, andam e saem — com as regras do domínio no meio
 * (ADR-0004).
 */
public sealed class FunisTests(ApiDeTestes api)
{
    private static readonly Requisito[] SoQuemDecide = [Requisito.QuemDecide];

    private static readonly Requisito[] ValorEQuemDecide = [Requisito.ValorEstimado, Requisito.QuemDecide];

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Gerente => api.Cliente(_construtora, Papeis.Gerente);

    private async Task<(string Funil, FunilResposta Lido)> ObraPublica()
    {
        var criado = await Gerente.Criar("/api/funis", Exemplos.FunilDeObraPublica).ComSucesso();

        return (criado.Headers.Location!.ToString(), await criado.Ler<FunilResposta>());
    }

    private static Guid Etapa(FunilResposta funil, string nome) =>
        funil.Etapas.Concat(funil.Arquivadas).Single(etapa => etapa.Nome == nome).Id;

    [Fact]
    public async Task O_funil_padrao_vem_pronto_da_primeira_conversa_a_obra_ganha_ou_perdida()
    {
        var criado = await Gerente.Criar("/api/funis/padrao", null).ComSucesso();
        var funil = await criado.Ler<FunilResposta>();

        Assert.Equal((HttpStatusCode.Created, "Vendas de obras"), (criado.StatusCode, funil.Nome));
        Assert.Equal(
            [
                ("Primeiro contato", TipoDeEtapa.Aberta, 10),
                ("Visita técnica", TipoDeEtapa.Aberta, 20),
                ("Orçamento em elaboração", TipoDeEtapa.Aberta, 40),
                ("Proposta enviada", TipoDeEtapa.Aberta, 60),
                ("Negociação", TipoDeEtapa.Aberta, 80),
                ("Ganha", TipoDeEtapa.Ganha, 100),
                ("Perdida", TipoDeEtapa.Perdida, 0),
            ],
            funil.Etapas.Select(etapa => (etapa.Nome, etapa.Tipo, etapa.Probabilidade)));
        Assert.Equal([Requisito.ValorEstimado, Requisito.PrevisaoDeFechamento], funil.Etapas[3].Requisitos);
        Assert.Equal([Requisito.QuemDecide], funil.Etapas[2].Requisitos);
        Assert.Empty(funil.Arquivadas);
    }

    [Fact]
    public async Task Os_requisitos_vao_e_voltam_como_lista_na_ordem_do_contrato()
    {
        var (_, funil) = await ObraPublica();

        Assert.Equal([], funil.Etapas[0].Requisitos);
        Assert.Equal([Requisito.ValorEstimado], funil.Etapas[1].Requisitos);
        Assert.Equal([Requisito.ValorEstimado, Requisito.PrevisaoDeFechamento], funil.Etapas[2].Requisitos);
        Assert.Equal(45, funil.Etapas[1].DiasEsperados);
    }

    [Fact]
    public async Task Requisito_que_o_contrato_nao_tem_e_400()
    {
        var resposta = await Gerente.Criar("/api/funis", """{"nome": "Obras", "etapas": [{"nome": "Contato", "probabilidade": 10, "requisitos": ["Orcamento"]}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Probabilidade_que_cai_e_requisito_na_primeira_sao_422_com_tudo_o_que_esta_errado()
    {
        var resposta = await Gerente.Criar("/api/funis", new
        {
            nome = "Reformas",
            etapas = new object[]
            {
                new { nome = "Vistoria", probabilidade = 30, requisitos = SoQuemDecide },
                new { nome = "Orçamento", probabilidade = 20 },
            },
        });
        var detalhe = (await resposta.Problema()).Detail;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("a primeira etapa não tem requisito", detalhe, StringComparison.Ordinal);
        Assert.Contains("a probabilidade não cai", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_etapa_nova_entra_no_lugar_que_a_probabilidade_indica()
    {
        var (funil, _) = await ObraPublica();

        var comRecurso = await Gerente.Enviar(HttpMethod.Post, $"{funil}/etapas", new { nome = "Recurso", probabilidade = 40 }, versao: 1)
            .LerComSucesso<FunilResposta>();

        Assert.Equal(
            ["Edital publicado", "Proposta entregue", "Recurso", "Habilitação", "Ganha", "Perdida"],
            comRecurso.Etapas.Select(etapa => etapa.Nome));
    }

    [Fact]
    public async Task Altera_a_etapa_aberta_e_so_renomeia_a_final()
    {
        var (funil, lido) = await ObraPublica();
        var proposta = Etapa(lido, "Proposta entregue");
        var ganha = Etapa(lido, "Ganha");

        var alterada = await Gerente
            .Enviar(HttpMethod.Put, $"{funil}/etapas/{proposta}", new { nome = "Proposta protocolada", probabilidade = 35, diasEsperados = 30, requisitos = ValorEQuemDecide }, versao: 1)
            .LerComSucesso<FunilResposta>();
        var finalAlterada = await Gerente.Enviar(HttpMethod.Put, $"{funil}/etapas/{ganha}", new { nome = "Contratada", probabilidade = 99 }, versao: 2);
        var finalRenomeada = await Gerente.Enviar(HttpMethod.Patch, $"{funil}/etapas/{ganha}", new { nome = "Contrato assinado" }, versao: 2)
            .LerComSucesso<FunilResposta>();

        var etapa = alterada.Etapas[1];
        Assert.Equal(
            (proposta, "Proposta protocolada", TipoDeEtapa.Aberta, 35, (int?)30),
            (etapa.Id, etapa.Nome, etapa.Tipo, etapa.Probabilidade, etapa.DiasEsperados));
        Assert.Equal(ValorEQuemDecide, etapa.Requisitos);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, finalAlterada.StatusCode);
        Assert.Equal(("Contrato assinado", 100), (finalRenomeada.Etapas[^2].Nome, finalRenomeada.Etapas[^2].Probabilidade));
    }

    [Fact]
    public async Task Move_a_etapa_se_a_probabilidade_continua_sem_cair()
    {
        var (funil, lido) = await ObraPublica();
        await Gerente.Enviar(HttpMethod.Put, $"{funil}/etapas/{Etapa(lido, "Habilitação")}", new { nome = "Habilitação", probabilidade = 30 }, versao: 1).ComSucesso();

        var movida = await Gerente.Enviar(HttpMethod.Put, $"{funil}/etapas/{Etapa(lido, "Habilitação")}/posicao", new { posicao = 2 }, versao: 2)
            .LerComSucesso<FunilResposta>();
        var foraDoFunil = await Gerente.Enviar(HttpMethod.Put, $"{funil}/etapas/{Etapa(lido, "Habilitação")}/posicao", new { posicao = 4 }, versao: 3);
        var antesDaPrimeira = await Gerente.Enviar(HttpMethod.Put, $"{funil}/etapas/{Etapa(lido, "Habilitação")}/posicao", new { posicao = 1 }, versao: 3);

        Assert.Equal(["Edital publicado", "Habilitação", "Proposta entregue"], movida.Etapas.Take(3).Select(etapa => etapa.Nome));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foraDoFunil.StatusCode);
        Assert.Contains("vão da 1 à 3", (await foraDoFunil.Problema()).Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, antesDaPrimeira.StatusCode);
    }

    [Fact]
    public async Task Arquiva_e_reativa_a_etapa_sem_perder_a_identidade()
    {
        var (funil, lido) = await ObraPublica();
        var habilitacao = Etapa(lido, "Habilitação");

        var arquivada = await Gerente.Enviar(HttpMethod.Post, $"{funil}/etapas/{habilitacao}/arquivamento", versao: 1).LerComSucesso<FunilResposta>();
        var reativada = await Gerente.Enviar(HttpMethod.Post, $"{funil}/etapas/{habilitacao}/reativacao", versao: 2).LerComSucesso<FunilResposta>();
        var deNovo = await Gerente.Enviar(HttpMethod.Post, $"{funil}/etapas/{habilitacao}/reativacao", versao: 3);

        Assert.Equal(habilitacao, Assert.Single(arquivada.Arquivadas).Id);
        Assert.DoesNotContain(arquivada.Etapas, etapa => etapa.Id == habilitacao);
        Assert.Empty(reativada.Arquivadas);
        Assert.Equal(habilitacao, reativada.Etapas[2].Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deNovo.StatusCode);
    }

    [Fact]
    public async Task Etapa_que_o_funil_nao_tem_e_404()
    {
        var (funil, _) = await ObraPublica();
        var inventada = Guid.CreateVersion7();

        var resposta = await Gerente.Enviar(HttpMethod.Post, $"{funil}/etapas/{inventada}/arquivamento", versao: 1);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal($"O funil {funil.Split('/')[^1]} não tem a etapa {inventada}.", (await resposta.Problema()).Detail);
    }

    [Fact]
    public async Task Renomeia_lista_e_remove()
    {
        var (funil, _) = await ObraPublica();

        await Gerente.Enviar(HttpMethod.Patch, funil, new { nome = "Licitações" }, versao: 1).ComSucesso();
        var listados = await Gerente.LerComSucesso<List<ResumoResposta>>("/api/funis");
        var removido = await Gerente.Enviar(HttpMethod.Delete, funil, versao: 2);

        Assert.Equal([("Licitações", 2)], listados.Select(resumo => (resumo.Nome, resumo.Versao)));
        Assert.Equal(HttpStatusCode.NoContent, removido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Gerente.GetAsync(funil, Cancelamento)).StatusCode);
    }
}
