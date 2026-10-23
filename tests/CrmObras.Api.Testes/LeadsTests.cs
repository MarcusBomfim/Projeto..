using System.Net;
using CrmObras.Api.Recursos;
using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Recursos.Leads;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Leads;

namespace CrmObras.Api.Testes;

/*
 * O lead pela API, da chegada à conversão: as perguntas da qualificação, o
 * descarte com motivo, a busca de duplicados no servidor e a conversão que
 * grava o lead e a conta juntos (ADR-0003).
 */
public sealed class LeadsTests(ApiDeTestes api)
{
    private static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 30, 0, TimeSpan.FromHours(-3));

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Vendedor => api.Cliente(_construtora, Papeis.Vendedor);

    private Task<HttpResponseMessage> Avaliar(string lead, object avaliacao, int versao) =>
        Vendedor.Enviar(HttpMethod.Put, $"{lead}/avaliacao", avaliacao, versao);

    private Task<HttpResponseMessage> Converter(string lead, object conversao, int versao) =>
        Vendedor.Enviar(HttpMethod.Post, $"{lead}/conversao", conversao, versao);

    /// <summary>Um lead já qualificado, na versão 3: criado, avaliado, qualificado.</summary>
    private async Task<string> Qualificado(object lead)
    {
        var criado = (await Vendedor.Criar("/api/leads", lead).ComSucesso()).Headers.Location!.ToString();
        await Avaliar(criado, Exemplos.AvaliacaoQueQualifica, versao: 1).ComSucesso();
        await Vendedor.Enviar(HttpMethod.Post, $"{criado}/qualificacao", versao: 2).ComSucesso();

        return criado;
    }

    private async Task<int> QuantasContas() => (await Vendedor.LerComSucesso<List<ResumoResposta>>("/api/contas")).Count;

    [Fact]
    public async Task O_lead_chega_novo_na_hora_da_construtora_e_sem_avaliacao()
    {
        api.AcertarRelogio(_construtora, Segunda);

        var criado = await Vendedor.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso();
        var lead = await criado.Ler<LeadResposta>();

        Assert.Equal((HttpStatusCode.Created, "\"1\""), (criado.StatusCode, criado.Headers.ETag?.Tag));
        Assert.Equal(
            ("Marina Costa", Exemplos.CnpjDaFilial, "marina.costa@rotasul.com.br", "(19) 99876-5432", OrigemDoLead.Site),
            (lead.Nome, lead.Documento, lead.Email, lead.Telefone, lead.Origem));
        Assert.Equal((SituacaoDoLead.Novo, Segunda), (lead.Situacao, lead.ChegouEm));
        Assert.Equal(TimeSpan.FromHours(-3), lead.ChegouEm.Offset);
        Assert.False(lead.Avaliacao.Qualifica);
        Assert.Equal(3, lead.Avaliacao.Pendencias.Count);
        Assert.Null(lead.Descarte);
        Assert.Null(lead.Conversao);
    }

    [Fact]
    public async Task Avalia_e_so_qualifica_quando_as_respostas_permitem()
    {
        api.AcertarRelogio(_construtora, Segunda);
        var lead = (await Vendedor.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();

        var avaliado = await Avaliar(lead, new { orcamento = "Sim", falaComQuemDecide = "Nao", necessidade = "NaoSabido", prazo = "Nao" }, versao: 1)
            .LerComSucesso<LeadResposta>();
        var cedo = await Vendedor.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 2);
        await Avaliar(lead, Exemplos.AvaliacaoQueQualifica, versao: 2).ComSucesso();
        var qualificado = await Vendedor.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 3).LerComSucesso<LeadResposta>();

        Assert.Equal(SituacaoDoLead.EmQualificacao, avaliado.Situacao);
        Assert.Equal(["a necessidade da obra confirmada", "falar com quem decide ou ter o prazo da obra"], avaliado.Avaliacao.Pendencias);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cedo.StatusCode);
        Assert.Equal(
            "O lead Marina Costa ainda não se qualifica: falta a necessidade da obra confirmada e falar com quem decide ou ter o prazo da obra.",
            (await cedo.Problema()).Detail);
        Assert.Equal((SituacaoDoLead.Qualificado, Segunda), (qualificado.Situacao, qualificado.QualificadoEm));
    }

    [Fact]
    public async Task Descarta_com_o_motivo_e_reabre()
    {
        api.AcertarRelogio(_construtora, Segunda);
        var lead = (await Vendedor.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();

        var semDizerQual = await Vendedor.Enviar(HttpMethod.Post, $"{lead}/descarte", new { motivo = "Outro" }, versao: 1);
        var descartado = await Vendedor.Enviar(HttpMethod.Post, $"{lead}/descarte", new { motivo = "PrazoDistante", observacao = "Obra só em 2028." }, versao: 1)
            .LerComSucesso<LeadResposta>();
        var reaberto = await Vendedor.Enviar(HttpMethod.Post, $"{lead}/reabertura", versao: 2).LerComSucesso<LeadResposta>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, semDizerQual.StatusCode);
        Assert.Equal(SituacaoDoLead.Descartado, descartado.Situacao);
        Assert.Equal(new DescarteResposta(MotivoDoDescarte.PrazoDistante, "Obra só em 2028.", Segunda), descartado.Descarte);
        Assert.Equal(SituacaoDoLead.EmQualificacao, reaberto.Situacao);
        Assert.Null(reaberto.Descarte);
    }

    [Fact]
    public async Task Lista_os_leads_de_uma_situacao_do_que_chegou_primeiro()
    {
        api.AcertarRelogio(_construtora, Segunda.AddHours(1));
        var depois = await Vendedor.Criar("/api/leads", new { nome = "Bruno", origem = "Telefone", telefone = "(11) 3333-4444" }).IdCriado();
        api.AcertarRelogio(_construtora, Segunda);
        var antes = await Vendedor.Criar("/api/leads", new { nome = "Carla", origem = "Site", email = "carla@alfa.com.br" }).IdCriado();
        var descartado = await Vendedor.Criar("/api/leads", new { nome = "Davi", origem = "Site", email = "davi@beta.com.br" }).IdCriado();
        await Vendedor.Enviar(HttpMethod.Post, $"/api/leads/{descartado}/descarte", new { motivo = "SemInteresse" }, versao: 1).ComSucesso();

        var novos = await Vendedor.LerComSucesso<List<ResumoDoLeadResposta>>("/api/leads?situacao=Novo");
        var descartados = await Vendedor.LerComSucesso<List<ResumoDoLeadResposta>>("/api/leads?situacao=Descartado");

        Assert.Equal([(antes, 1), (depois, 1)], novos.Select(lead => (lead.Id, lead.Versao)));
        Assert.Equal([(descartado, 2)], descartados.Select(lead => (lead.Id, lead.Versao)));
    }

    [Fact]
    public async Task Os_duplicados_dizem_o_que_cada_conta_tem_em_comum_com_o_lead()
    {
        var matriz = await Vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul()).IdCriado();
        await Vendedor.Criar("/api/contas", new { nome = "Construtora Alfa" }).ComSucesso();
        var lead = (await Vendedor.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();

        var duplicados = await Vendedor.LerComSucesso<List<PossivelDuplicadoResposta>>($"{lead}/duplicados");

        var duplicado = Assert.Single(duplicados);
        Assert.Equal((matriz, "Logística Rota Sul Ltda."), (duplicado.Conta, duplicado.NomeDaConta));
        Assert.Equal(
            [Semelhanca.MesmaEmpresa, Semelhanca.MesmoNome, Semelhanca.MesmoDominio],
            duplicado.Indicios.Select(indicio => indicio.Semelhanca));
        Assert.All(duplicado.Indicios, indicio => Assert.False(string.IsNullOrWhiteSpace(indicio.Motivo)));
    }

    [Fact]
    public async Task Converte_em_conta_nova_e_grava_o_lead_e_a_conta()
    {
        api.AcertarRelogio(_construtora, Segunda);
        var lead = await Qualificado(Exemplos.LeadDaMarina());

        var convertido = await Converter(lead, new { }, versao: 3).ComSucesso();
        var conversao = await convertido.Ler<ConversaoResposta>();

        Assert.Equal("\"4\"", convertido.Headers.ETag?.Tag);
        Assert.Equal(SituacaoDoLead.Convertido, conversao.Lead.Situacao);
        Assert.Equal(new ConversaoDoLeadResposta(conversao.Conta.Id, conversao.Conta.Principal!.Value, Segunda), conversao.Lead.Conversao);
        Assert.Equal(1, conversao.VersaoDaConta);
        Assert.Equal(("Logística Rota Sul Ltda.", Exemplos.CnpjDaFilial), (conversao.Conta.Nome, conversao.Conta.Documento));
        Assert.Equal("marina.costa@rotasul.com.br", Assert.Single(conversao.Conta.Contatos).Email);
        Assert.Equivalent(conversao.Conta, await Vendedor.LerComSucesso<ContaResposta>($"/api/contas/{conversao.Conta.Id}"), strict: true);
        Assert.Equivalent(conversao.Lead, await Vendedor.LerComSucesso<LeadResposta>(lead), strict: true);
    }

    [Fact]
    public async Task Sem_corpo_tambem_e_conta_nova()
    {
        var lead = await Qualificado(Exemplos.LeadDaMarina(documento: null));

        var convertido = await Converter(lead, null!, versao: 3).LerComSucesso<ConversaoResposta>();

        Assert.Equal(SituacaoDoLead.Convertido, convertido.Lead.Situacao);
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task Com_conta_do_mesmo_documento_a_conversao_em_conta_nova_e_recusada_e_nada_muda()
    {
        await Vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul(Exemplos.CnpjDaFilial)).ComSucesso();
        var lead = await Qualificado(Exemplos.LeadDaMarina());

        var recusada = await Converter(lead, new { }, versao: 3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, recusada.StatusCode);
        Assert.Equal(
            $"Já existe a conta Logística Rota Sul Ltda. com o documento {Exemplos.CnpjDaFilial}: converta o lead nela.",
            (await recusada.Problema()).Detail);
        Assert.Equal(1, await QuantasContas());
        Assert.Equal(3, (await Vendedor.GetAsync(lead, Cancelamento)).Versao());
    }

    [Fact]
    public async Task Converte_na_conta_que_ja_existe_como_contato_dela()
    {
        var matriz = (await Vendedor.Criar("/api/contas", new { nome = "Logística Rota Sul Ltda." }).ComSucesso()).Headers.Location!.ToString();
        var lead = await Qualificado(Exemplos.LeadDaMarina(documento: Exemplos.CnpjDaMatriz));

        var conversao = await Converter(lead, new { conta = Guid.Parse(matriz.Split('/')[^1]) }, versao: 3).LerComSucesso<ConversaoResposta>();
        var conta = await Vendedor.GetAsync(matriz, Cancelamento);

        Assert.Equal(2, conversao.VersaoDaConta);
        Assert.Equal(Exemplos.CnpjDaMatriz, conversao.Conta.Documento);
        Assert.Equal(conversao.Lead.Conversao!.Contato, Assert.Single(conversao.Conta.Contatos).Id);
        Assert.Equal(2, conta.Versao());
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task Conta_que_nao_existe_e_404_e_o_lead_fica_como_estava()
    {
        var lead = await Qualificado(Exemplos.LeadDaMarina());
        var inventada = Guid.CreateVersion7();

        var resposta = await Converter(lead, new { conta = inventada }, versao: 3);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal($"Não existe a conta {inventada}.", (await resposta.Problema()).Detail);
        Assert.Equal(SituacaoDoLead.Qualificado, (await Vendedor.LerComSucesso<LeadResposta>(lead)).Situacao);
    }

    [Fact]
    public async Task De_conversoes_simultaneas_do_mesmo_lead_so_uma_fica_e_com_uma_conta_so()
    {
        // As que perdem chegaram a gravar a conta nova — e a desfazem junto
        // com o lead, na mesma transação.
        var lead = await Qualificado(Exemplos.LeadDaMarina(documento: null));

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Converter(lead, new { }, versao: 3)));

        Assert.Equal(1, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, respostas.Count(resposta => resposta.StatusCode == HttpStatusCode.PreconditionFailed));
        Assert.Equal(1, await QuantasContas());
    }

    [Fact]
    public async Task O_lead_convertido_nao_volta_a_qualificacao()
    {
        var lead = await Qualificado(Exemplos.LeadDaMarina());
        await Converter(lead, new { }, versao: 3).ComSucesso();

        var resposta = await Avaliar(lead, Exemplos.AvaliacaoQueQualifica, versao: 4);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("O lead Marina Costa já virou cliente, e não pode ser avaliado: a história segue na conta.", (await resposta.Problema()).Detail);
    }

    [Fact]
    public async Task A_gerencia_remove_o_lead_cadastrado_errado()
    {
        var lead = (await Vendedor.Criar("/api/leads", Exemplos.LeadDaMarina()).ComSucesso()).Headers.Location!.ToString();

        var removido = await api.Cliente(_construtora, Papeis.Gerente).Enviar(HttpMethod.Delete, lead, versao: 1);

        Assert.Equal(HttpStatusCode.NoContent, removido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Vendedor.GetAsync(lead, Cancelamento)).StatusCode);
    }
}
