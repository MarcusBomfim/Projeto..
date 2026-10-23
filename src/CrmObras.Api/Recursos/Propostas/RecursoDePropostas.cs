using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Propostas;

/// <summary>
/// As propostas: o rascunho, as versões, o desconto aprovado por quem tem
/// alçada, o envio, e a resposta do cliente — o aceite ganha a venda (ADR-0007).
/// </summary>
public sealed class RecursoDePropostas : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var propostas = api.MapGroup("/propostas").WithTags("Propostas");

        propostas.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        propostas.MapPost("/", Criar).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        propostas.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        propostas.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        propostas.MapPut("/{id:guid}/rascunho", Redigir).RequireAuthorization(Politicas.Vender);
        propostas.MapPost("/{id:guid}/versoes", NovaVersao).RequireAuthorization(Politicas.Vender);
        propostas.MapPost("/{id:guid}/aprovacao", AprovarDesconto).RequireAuthorization(Politicas.Gerir);
        propostas.MapPost("/{id:guid}/envio", Enviar).RequireAuthorization(Politicas.Vender);
        propostas.MapPost("/{id:guid}/aceite", Aceitar).RequireAuthorization(Politicas.Vender);
        propostas.MapPost("/{id:guid}/recusa", Recusar).RequireAuthorization(Politicas.Vender);
    }

    /// <summary>As propostas de uma venda, da mais antiga: <c>?oportunidade=</c>.</summary>
    private static async Task<Ok<IReadOnlyList<ResumoDaPropostaResposta>>> Listar(
        Guid oportunidade,
        RepositorioDePropostas repositorio,
        CancellationToken cancelamento)
    {
        var lidas = await repositorio.DaOportunidade(new OportunidadeId(oportunidade), cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoDaPropostaResposta>>([.. lidas.Select(ResumoDaPropostaResposta.De)]);
    }

    private static async Task<Created<PropostaResposta>> Criar(
        NovaProposta nova,
        RepositorioDePropostas repositorio,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var venda = (await Leituras.Oportunidade(oportunidades, nova.Oportunidade, cancelamento)).Agregado;
        var agora = fuso.Agora();
        var proposta = new Proposta(venda, nova.Versao.ParaDados(), agora);

        Etiquetas.Escrever(http, await repositorio.Adicionar(proposta, cancelamento));

        return TypedResults.Created($"/api/propostas/{proposta.Id}", PropostaResposta.De(proposta, agora));
    }

    private static async Task<Results<Ok<PropostaResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDePropostas repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Proposta(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(PropostaResposta.De(lida.Agregado, fuso.Agora()));
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDePropostas repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new PropostaId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    /// <summary>Troca o conteúdo do rascunho. Desfaz a aprovação do desconto, se havia.</summary>
    private static Task<Ok<PropostaResposta>> Redigir(
        Guid id,
        ConteudoDaVersao conteudo,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (proposta, _) => proposta.Redigir(conteudo.ParaDados()), cancelamento);

    /// <summary>Uma versão nova, em rascunho, igual à última enviada — para mudar o que o cliente já viu.</summary>
    private static Task<Ok<PropostaResposta>> NovaVersao(
        Guid id,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (proposta, _) => proposta.NovaVersao(), cancelamento);

    /// <summary>Quem pede aprova o desconto do rascunho, se ele cabe na alçada do cargo dele.</summary>
    private static Task<Ok<PropostaResposta>> AprovarDesconto(
        Guid id,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Alcadas alcadas,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            fuso,
            http,
            (proposta, agora) => proposta.AprovarDesconto(QuemPede.Comercial(http.User), alcadas, agora),
            cancelamento);

    /// <summary>Quem pede manda o rascunho ao cliente; o desconto acima da alçada dele precisa estar aprovado.</summary>
    private static Task<Ok<PropostaResposta>> Enviar(
        Guid id,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Alcadas alcadas,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            fuso,
            http,
            (proposta, agora) => proposta.Enviar(QuemPede.Comercial(http.User), alcadas, agora),
            cancelamento);

    /// <summary>
    /// O cliente aceita a versão que está com ele, e a venda é ganha pelo
    /// total dela. A proposta e a venda se gravam juntas, numa transação: a
    /// versão no <c>If-Match</c> é a da proposta; a da venda é a que se leu
    /// aqui, e quem gravar nela no meio faz o aceite ser recusado com 412.
    /// </summary>
    private static async Task<Ok<AceiteResposta>> Aceitar(
        Guid id,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        RepositorioDeOportunidades oportunidades,
        RepositorioDeFunis funis,
        ContextoDoBanco contexto,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Proposta(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A proposta {id}");

        var proposta = lida.Agregado;
        var venda = await Leituras.Oportunidade(oportunidades, proposta.Oportunidade.Valor, cancelamento);
        var funil = (await Leituras.Funil(funis, venda.Agregado.Funil.Valor, cancelamento)).Agregado;
        var agora = fuso.Agora();

        FechamentoPelaProposta.Aceitar(proposta, venda.Agregado, funil, agora);

        var (versaoDaProposta, versaoDaVenda) = await contexto.NumaTransacao(
            async () => (await repositorio.Atualizar(proposta, versao.Valor, cancelamento), await oportunidades.Atualizar(venda.Agregado, venda.Versao, cancelamento)),
            cancelamento);

        Etiquetas.Escrever(http, versaoDaProposta);

        return TypedResults.Ok(new AceiteResposta(PropostaResposta.De(proposta, agora), OportunidadeResposta.De(venda.Agregado), versaoDaVenda));
    }

    private static Task<Ok<PropostaResposta>> Recusar(
        Guid id,
        Recusa recusa,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (proposta, agora) => proposta.Recusar(recusa.Motivo, agora), cancelamento);

    private static async Task<Ok<PropostaResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDePropostas repositorio,
        Fuso fuso,
        HttpContext http,
        Action<Proposta, DateTimeOffset> alteracao,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Proposta(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A proposta {id}");

        var agora = fuso.Agora();
        alteracao(lida.Agregado, agora);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lida.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(PropostaResposta.De(lida.Agregado, agora));
    }
}
