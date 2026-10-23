using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Leads;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Leads;

/// <summary>Os interessados: a chegada, a qualificação, o descarte, os duplicados e a conversão em cliente (ADR-0003).</summary>
public sealed class RecursoDeLeads : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var leads = api.MapGroup("/leads").WithTags("Leads");

        leads.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        leads.MapPost("/", Criar).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        leads.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        leads.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        leads.MapPut("/{id:guid}/avaliacao", Avaliar).RequireAuthorization(Politicas.Vender);
        leads.MapPost("/{id:guid}/qualificacao", Qualificar).RequireAuthorization(Politicas.Vender);
        leads.MapPost("/{id:guid}/descarte", Descartar).RequireAuthorization(Politicas.Vender);
        leads.MapPost("/{id:guid}/reabertura", Reabrir).RequireAuthorization(Politicas.Vender);

        leads.MapGet("/{id:guid}/duplicados", Duplicados).RequireAuthorization(Politicas.Consultar);
        leads.MapPost("/{id:guid}/conversao", Converter).RequireAuthorization(Politicas.Vender);
    }

    /// <summary>Os leads numa situação, do que chegou primeiro: <c>?situacao=Novo</c>.</summary>
    private static async Task<Ok<IReadOnlyList<ResumoDoLeadResposta>>> Listar(
        string situacao,
        RepositorioDeLeads repositorio,
        CancellationToken cancelamento)
    {
        var lidos = await repositorio.NaSituacao(situacao.ComoOpcao<SituacaoDoLead>(nameof(situacao)), cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoDoLeadResposta>>(
        [
            .. lidos.Select(lido => new ResumoDoLeadResposta(
                lido.Agregado.Id.Valor,
                lido.Agregado.Nome,
                lido.Agregado.Empresa,
                lido.Agregado.Origem,
                lido.Agregado.Situacao,
                lido.Agregado.ChegouEm,
                lido.Versao)),
        ]);
    }

    private static async Task<Created<LeadResposta>> Criar(
        NovoLead novo,
        RepositorioDeLeads repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lead = new Lead(novo.ParaDados(), novo.Origem, novo.DetalheDaOrigem, fuso.Agora());

        Etiquetas.Escrever(http, await repositorio.Adicionar(lead, cancelamento));

        return TypedResults.Created($"/api/leads/{lead.Id}", LeadResposta.De(lead));
    }

    private static async Task<Results<Ok<LeadResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeLeads repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Lead(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return Etiquetas.ClienteJaTem(http, lido.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(LeadResposta.De(lido.Agregado));
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeLeads repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new LeadId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<LeadResposta>> Avaliar(
        Guid id,
        NovaAvaliacao avaliacao,
        VersaoLida versao,
        RepositorioDeLeads repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, lead => lead.Avaliar(avaliacao.ParaAvaliacao()), cancelamento);

    private static Task<Ok<LeadResposta>> Qualificar(
        Guid id,
        VersaoLida versao,
        RepositorioDeLeads repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, lead => lead.Qualificar(fuso.Agora()), cancelamento);

    private static Task<Ok<LeadResposta>> Descartar(
        Guid id,
        Descarte descarte,
        VersaoLida versao,
        RepositorioDeLeads repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            lead => lead.Descartar(descarte.Motivo, descarte.Observacao, fuso.Agora()),
            cancelamento);

    private static Task<Ok<LeadResposta>> Reabrir(
        Guid id,
        VersaoLida versao,
        RepositorioDeLeads repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, lead => lead.Reabrir(), cancelamento);

    /// <summary>As contas que podem ser o cliente do lead: o banco escolhe as candidatas, e o domínio diz o que cada uma tem em comum com ele.</summary>
    private static async Task<Ok<IReadOnlyList<PossivelDuplicadoResposta>>> Duplicados(
        Guid id,
        RepositorioDeLeads leads,
        RepositorioDeContas contas,
        CancellationToken cancelamento)
    {
        var lead = (await Leituras.Lead(leads, id, cancelamento)).Agregado;
        var duplicados = BuscaDeDuplicados.Do(lead, await contas.CandidatasADuplicado(lead, cancelamento));

        return TypedResults.Ok<IReadOnlyList<PossivelDuplicadoResposta>>([.. duplicados.Select(PossivelDuplicadoResposta.De)]);
    }

    /// <summary>
    /// O lead qualificado vira cliente — numa conta que já existe, ou numa
    /// nova. A conta nova só nasce depois de o servidor procurar os
    /// duplicados: se já há conta com o mesmo documento, a conversão é
    /// recusada, e o lead vira contato dela (ADR-0003).
    /// </summary>
    /// <remarks>
    /// O lead e a conta se gravam juntos, numa transação. A versão no
    /// <c>If-Match</c> é a do lead; a da conta existente é a que se leu aqui,
    /// e quem gravar nela no meio faz a conversão ser recusada com 412.
    /// </remarks>
    private static async Task<Ok<ConversaoResposta>> Converter(
        Guid id,
        Conversao? conversao,
        VersaoLida versao,
        RepositorioDeLeads leads,
        RepositorioDeContas contas,
        ContextoDoBanco contexto,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Lead(leads, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O lead {id}");

        var lead = lido.Agregado;
        var agora = fuso.Agora();

        var (conta, versaoDaConta, versaoDoLead) = await contexto.NumaTransacao(
            async () =>
            {
                Conta cliente;
                int gravada;

                if (conversao?.Conta is { } existente)
                {
                    var lida = await Leituras.Conta(contas, existente, cancelamento);
                    cliente = lida.Agregado;
                    lead.ConverterNaConta(cliente, agora);
                    gravada = await contas.Atualizar(cliente, lida.Versao, cancelamento);
                }
                else
                {
                    var duplicados = BuscaDeDuplicados.Do(lead, await contas.CandidatasADuplicado(lead, cancelamento));
                    cliente = lead.ConverterEmContaNova(duplicados, agora);
                    gravada = await contas.Adicionar(cliente, cancelamento);
                }

                return (cliente, gravada, await leads.Atualizar(lead, versao.Valor, cancelamento));
            },
            cancelamento);

        Etiquetas.Escrever(http, versaoDoLead);

        return TypedResults.Ok(new ConversaoResposta(LeadResposta.De(lead), ContaResposta.De(conta), versaoDaConta));
    }

    private static async Task<Ok<LeadResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeLeads repositorio,
        HttpContext http,
        Action<Lead> alteracao,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Lead(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O lead {id}");

        alteracao(lido.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lido.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(LeadResposta.De(lido.Agregado));
    }
}
