using System.Net;
using CrmObras.Api.Recursos.Funis;
using CrmObras.Api.Recursos.Leads;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Api.Testes;

/*
 * A venda pela API: nasce na primeira etapa, em nome de quem pede; anda
 * pelo funil conferindo os requisitos; termina ganha ou perdida; e aparece
 * no quadro do funil (ADR-0004, ADR-0005).
 */
public sealed class OportunidadesTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly CenarioDeVenda _cenario = new(api);

    private static Guid Etapa(FunilResposta funil, string nome) => funil.Etapas.Single(etapa => etapa.Nome == nome).Id;

    private Task<HttpResponseMessage> Mover(Guid venda, Guid etapa, int versao) =>
        _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/oportunidades/{venda}/etapa", new { etapa }, versao);

    [Fact]
    public async Task A_venda_nasce_na_primeira_etapa_em_nome_de_quem_pede()
    {
        _cenario.Relogio(CenarioDeVenda.Segunda);
        var (filial, _, sergio) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();

        var criada = await _cenario.Carlos.Criar("/api/oportunidades", new
        {
            conta = filial,
            funil = funil.Id,
            titulo = "Galpão de 1.200 m² em Jundiaí",
            valorEstimado = 2_400_000.00m,
            previsaoDeFechamento = "2026-12-15",
            quemDecide = sergio,
        }).ComSucesso();
        var venda = await criada.Ler<OportunidadeResposta>();

        Assert.Equal((HttpStatusCode.Created, "\"1\""), (criada.StatusCode, criada.Headers.ETag?.Tag));
        Assert.Equal(("carlos.souza", SituacaoDaOportunidade.Aberta, funil.Etapas[0].Id), (venda.Responsavel, venda.Situacao, venda.Etapa));
        Assert.Equal((2_400_000.00m, new DateOnly(2026, 12, 15), sergio), (venda.ValorEstimado, venda.PrevisaoDeFechamento, venda.QuemDecide));
        Assert.Equal(new PassagemResposta(funil.Etapas[0].Id, CenarioDeVenda.Segunda, null), Assert.Single(venda.Passagens));
    }

    [Fact]
    public async Task Token_sem_login_nao_abre_venda_em_nome_de_ninguem()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var semLogin = api.ClienteComToken(Tokens.Emitir(
            _cenario.Construtora.ToString(), login: null, [Papeis.Vendedor], Tokens.Chave, Tokens.Emissor, Tokens.Audiencia, DateTime.UtcNow.AddHours(1)));

        var resposta = await semLogin.Criar("/api/oportunidades", new { conta = filial, funil = funil.Id, titulo = "Galpão" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("O token não traz o login de quem pede, na reivindicação preferred_username.", (await resposta.Problema()).Detail);
    }

    [Fact]
    public async Task Conta_ou_funil_que_nao_existe_e_404()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();

        var semConta = await _cenario.Carlos.Criar("/api/oportunidades", new { conta = Guid.CreateVersion7(), funil = funil.Id, titulo = "Galpão" });
        var semFunil = await _cenario.Carlos.Criar("/api/oportunidades", new { conta = filial, funil = Guid.CreateVersion7(), titulo = "Galpão" });

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (semConta.StatusCode, semFunil.StatusCode));
    }

    [Fact]
    public async Task Valor_fora_do_centavo_e_quem_decide_que_nao_e_da_conta_sao_422()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();

        var foraDoCentavo = await _cenario.Carlos.Criar("/api/oportunidades", new { conta = filial, funil = funil.Id, titulo = "Galpão", valorEstimado = 10.005m });
        var deFora = await _cenario.Carlos.Criar("/api/oportunidades", new { conta = filial, funil = funil.Id, titulo = "Galpão", quemDecide = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, foraDoCentavo.StatusCode);
        Assert.Contains("vai até o centavo", (await foraDoCentavo.Problema()).Detail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deFora.StatusCode);
    }

    [Fact]
    public async Task Anda_pelo_funil_so_com_o_que_cada_etapa_pede()
    {
        _cenario.Relogio(CenarioDeVenda.Segunda);
        var (filial, _, sergio) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var venda = (await _cenario.Venda(filial, funil)).Id;

        await Mover(venda, Etapa(funil, "Visita técnica"), versao: 1).ComSucesso();
        var semQuemDecide = await Mover(venda, Etapa(funil, "Orçamento em elaboração"), versao: 2);
        await _cenario.Carlos.Enviar(HttpMethod.Patch, $"/api/oportunidades/{venda}", new { quemDecide = sergio }, versao: 2).ComSucesso();
        var pulando = await Mover(venda, Etapa(funil, "Negociação"), versao: 3);
        var paraAGanha = await Mover(venda, Etapa(funil, "Ganha"), versao: 3);
        var noOrcamento = await Mover(venda, Etapa(funil, "Orçamento em elaboração"), versao: 3).LerComSucesso<OportunidadeResposta>();

        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Orçamento em elaboração, falta quem decide.",
            (await semQuemDecide.Problema()).Detail);
        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Negociação, falta o valor estimado e a previsão de fechamento.",
            (await pulando.Problema()).Detail);
        Assert.Equal("Ganha é etapa final: a venda chega nela ganha, com o valor fechado.", (await paraAGanha.Problema()).Detail);
        Assert.Equal(Etapa(funil, "Orçamento em elaboração"), noOrcamento.Etapa);
        Assert.Equal(3, noOrcamento.Passagens.Count);
    }

    [Fact]
    public async Task Ganha_nao_muda_mais_e_perdida_reabre()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var ganha = (await _cenario.Venda(filial, funil, "Galpão")).Id;
        var perdida = (await _cenario.Venda(filial, funil, "Portaria")).Id;

        var fechada = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{ganha}/ganho", new { valorFechado = 1_843_019.20m }, versao: 1)
            .LerComSucesso<OportunidadeResposta>();
        var depoisDeGanha = await _cenario.Carlos.Enviar(HttpMethod.Patch, $"/api/oportunidades/{ganha}", new { valorEstimado = 2_000_000m }, versao: 2);
        var semConcorrente = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{perdida}/perda", new { motivo = "ConcorrenteEscolhido" }, versao: 1);
        var perda = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{perdida}/perda", new { motivo = "ConcorrenteEscolhido", concorrente = "Construtora Beta" }, versao: 1)
            .LerComSucesso<OportunidadeResposta>();
        var reaberta = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{perdida}/reabertura", new { etapa = Etapa(funil, "Visita técnica") }, versao: 2)
            .LerComSucesso<OportunidadeResposta>();

        Assert.Equal((SituacaoDaOportunidade.Ganha, 1_843_019.20m, Etapa(funil, "Ganha")), (fechada.Situacao, fechada.ValorFechado, fechada.Etapa));
        Assert.Equal("A oportunidade Galpão foi ganha: a venda está fechada, e não muda mais.", (await depoisDeGanha.Problema()).Detail);
        Assert.Equal("Perda para o concorrente diz qual concorrente.", (await semConcorrente.Problema()).Detail);
        Assert.Equal(new PerdaResposta(MotivoDaPerda.ConcorrenteEscolhido, "Construtora Beta", null), perda.Perda);
        Assert.Equal((SituacaoDaOportunidade.Aberta, Etapa(funil, "Visita técnica")), (reaberta.Situacao, reaberta.Etapa));
        Assert.Null(reaberta.Perda);
    }

    [Fact]
    public async Task A_venda_do_lead_nasce_na_conta_dele_com_quem_decide_se_ele_decide()
    {
        var funil = await _cenario.FunilPadrao();
        var lead = (await _cenario.Carlos.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();
        var naoConvertido = await _cenario.Carlos.Criar("/api/oportunidades/do-lead", new { lead = Guid.Parse(lead.Split('/')[^1]), funil = funil.Id });
        await _cenario.Carlos.Enviar(HttpMethod.Put, $"{lead}/avaliacao", Exemplos.AvaliacaoQueQualifica, versao: 1).ComSucesso();
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 2).ComSucesso();
        var conversao = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{lead}/conversao", new { }, versao: 3).LerComSucesso<ConversaoResposta>();

        var venda = await _cenario.Carlos.Criar("/api/oportunidades/do-lead", new { lead = conversao.Lead.Id, funil = funil.Id }).LerComSucesso<OportunidadeResposta>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, naoConvertido.StatusCode);
        Assert.Equal("O lead Marina Costa ainda não virou cliente: converta-o antes de abrir a venda.", (await naoConvertido.Problema()).Detail);
        Assert.Equal(
            (conversao.Conta.Id, "Galpão de 1.200 m² em Campinas", conversao.Lead.Id, conversao.Lead.Conversao!.Contato, "carlos.souza"),
            (venda.Conta, venda.Titulo, venda.Lead, venda.QuemDecide, venda.Responsavel));
    }

    [Fact]
    public async Task O_quadro_mostra_cada_coluna_com_os_totais_e_a_venda_parada()
    {
        _cenario.Relogio(CenarioDeVenda.Segunda);
        var (filial, _, sergio) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var galpao = await _cenario.Carlos.Criar("/api/oportunidades", new
        {
            conta = filial,
            funil = funil.Id,
            titulo = "Galpão",
            valorEstimado = 2_400_000m,
            previsaoDeFechamento = "2026-12-15",
            quemDecide = sergio,
        }).IdCriado();
        await Mover(galpao, Etapa(funil, "Proposta enviada"), versao: 1).ComSucesso();

        // Dezesseis dias depois: a Proposta enviada espera quinze, e o galpão passou deles. A portaria acabou de chegar.
        _cenario.Relogio(CenarioDeVenda.Segunda.AddDays(16));
        await _cenario.Venda(filial, funil, "Portaria");

        var quadro = await _cenario.Leitor.LerComSucesso<QuadroResposta>($"/api/funis/{funil.Id}/quadro");

        Assert.Equal((2_400_000m, 1_440_000m), (quadro.Total, quadro.Ponderado));
        Assert.Equal(5, quadro.Colunas.Count);
        var primeiro = quadro.Colunas[0];
        Assert.Equal(("Portaria", false, 1, 0m), (Assert.Single(primeiro.Oportunidades).Titulo, primeiro.Oportunidades[0].Parada, primeiro.SemValor, primeiro.Total));
        var proposta = quadro.Colunas[3];
        var cartao = Assert.Single(proposta.Oportunidades);
        Assert.Equal(("Galpão", "Rota Sul Logística — Campinas", true, 2), (cartao.Titulo, cartao.NomeDaConta, cartao.Parada, cartao.Versao));
        Assert.Equal((2_400_000m, 1_440_000m), (proposta.Total, proposta.Ponderado));
    }

    [Fact]
    public async Task Lista_as_vendas_de_um_funil_ou_de_uma_conta()
    {
        var (filial, _, _) = await _cenario.Filial();
        var outra = await _cenario.Carlos.Criar("/api/contas", new { nome = "Construtora Alfa" }).IdCriado();
        var funil = await _cenario.FunilPadrao();
        var galpao = (await _cenario.Venda(filial, funil, "Galpão")).Id;
        var reforma = (await _cenario.Venda(outra, funil, "Reforma")).Id;

        var doFunil = await _cenario.Leitor.LerComSucesso<List<ResumoDaOportunidadeResposta>>($"/api/oportunidades?funil={funil.Id}");
        var daConta = await _cenario.Leitor.LerComSucesso<List<ResumoDaOportunidadeResposta>>($"/api/oportunidades?conta={outra}");
        var semFiltro = await _cenario.Leitor.GetAsync("/api/oportunidades", Cancelamento);
        var comOsDois = await _cenario.Leitor.GetAsync($"/api/oportunidades?funil={funil.Id}&conta={outra}", Cancelamento);

        Assert.Equal([galpao, reforma], doFunil.Select(venda => venda.Id));
        Assert.Equal([(reforma, 1)], daConta.Select(venda => (venda.Id, venda.Versao)));
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (semFiltro.StatusCode, comOsDois.StatusCode));
    }

    [Fact]
    public async Task So_a_gerencia_passa_a_venda_a_outro_vendedor_e_a_remove()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var venda = (await _cenario.Venda(filial, funil)).Id;

        var peloVendedor = await _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/oportunidades/{venda}/responsavel", new { responsavel = "ana.vendas" }, versao: 1);
        var pelaGerente = await _cenario.Helena.Enviar(HttpMethod.Put, $"/api/oportunidades/{venda}/responsavel", new { responsavel = "ana.vendas" }, versao: 1)
            .LerComSucesso<OportunidadeResposta>();
        var removerPeloVendedor = await _cenario.Carlos.Enviar(HttpMethod.Delete, $"/api/oportunidades/{venda}", versao: 2);
        var removerPelaGerente = await _cenario.Helena.Enviar(HttpMethod.Delete, $"/api/oportunidades/{venda}", versao: 2);

        Assert.Equal(HttpStatusCode.Forbidden, peloVendedor.StatusCode);
        Assert.Equal("ana.vendas", pelaGerente.Responsavel);
        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.NoContent), (removerPeloVendedor.StatusCode, removerPelaGerente.StatusCode));
    }

    [Fact]
    public async Task A_venda_com_historia_nao_sai_do_banco()
    {
        var (filial, _, _) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var venda = (await _cenario.Venda(filial, funil)).Id;
        await _cenario.Carlos.Criar("/api/atividades", new { conta = filial, tipo = "Ligacao", assunto = "Primeira conversa", previstaPara = "2026-10-20T10:00:00-03:00", oportunidade = venda })
            .ComSucesso();

        var removida = await _cenario.Helena.Enviar(HttpMethod.Delete, $"/api/oportunidades/{venda}", versao: 1);
        var contaRemovida = await _cenario.Helena.Enviar(HttpMethod.Delete, $"/api/contas/{filial}", versao: 1);

        Assert.Equal(HttpStatusCode.Conflict, removida.StatusCode);
        Assert.EndsWith("#em-uso", (await removida.Problema()).Type, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, contaRemovida.StatusCode);
    }
}
