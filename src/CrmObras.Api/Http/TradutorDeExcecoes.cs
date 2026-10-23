using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Http;

/// <summary>
/// Cada exceção que escapa de um endpoint vira o problema correspondente do
/// catálogo (ADR-0014). Os endpoints não tratam erro: lançam, e a tradução
/// é uma só, aqui.
/// </summary>
/// <remarks>
/// A mensagem das exceções do domínio e da persistência é escrita para gente
/// e vai no detalhe. Erro interno não: o detalhe dele fica nos registros, com
/// o id do rastreio, e quem pediu recebe só o id.
/// </remarks>
internal sealed partial class TradutorDeExcecoes(IProblemDetailsService problemas, ILogger<TradutorDeExcecoes> registro) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (problema, detalhe) = Traduzir(exception);

        if (problema.Status >= 500)
        {
            PedidoFalhou(registro, exception, httpContext.Request.Method, httpContext.Request.Path.ToString());
        }

        var detalhes = problema.Detalhar(detalhe);

        if (exception is ConflitoDeConcorrencia { VersaoAtual: { } atual })
        {
            // A versão que está no banco, no corpo — o middleware de exceções
            // tira a ETag de toda resposta de erro, para ninguém guardar erro
            // em cache.
            detalhes.Extensions["versaoAtual"] = atual;
        }

        httpContext.Response.StatusCode = problema.Status;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = detalhes,
        });
    }

    private static (Problema Problema, string? Detalhe) Traduzir(Exception excecao) => excecao switch
    {
        ExcecaoDeDominio => (Problemas.RegraDoNegocio, excecao.Message),
        AgregadoNaoEncontrado => (Problemas.NaoEncontrado, excecao.Message),
        AgregadoJaExiste => (Problemas.JaExiste, excecao.Message),
        AgregadoEmUso => (Problemas.EmUso, excecao.Message),
        ConflitoDeConcorrencia => (Problemas.VersaoDesatualizada, excecao.Message),
        ChaveDeIdempotenciaReutilizada => (Problemas.ChaveReutilizada, excecao.Message),
        VersaoNaoInformada => (Problemas.VersaoAusente, excecao.Message),
        PedidoInvalido => (Problemas.PedidoInvalido, excecao.Message),
        BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } => (Problemas.TipoNaoSuportado, excecao.Message),
        BadHttpRequestException erro => (Problemas.PedidoInvalido, Explicar(erro)),
        _ => (Problemas.ErroInterno, null),
    };

    /// <summary>O que estava errado no corpo: a mensagem do leitor de JSON diz qual campo e onde.</summary>
    private static string Explicar(BadHttpRequestException erro) =>
        erro.InnerException is JsonException json
            ? $"O corpo não é o JSON esperado: {json.Message}"
            : erro.Message;

    [LoggerMessage(Level = LogLevel.Error, Message = "O pedido {Metodo} {Caminho} falhou.")]
    private static partial void PedidoFalhou(ILogger registro, Exception excecao, string metodo, string caminho);
}
