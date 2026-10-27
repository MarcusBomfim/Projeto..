using Microsoft.AspNetCore.Mvc;

namespace CrmObras.Api.Http;

/// <summary>Um tipo de problema que a API devolve: o status, o tipo (um link para a explicação) e o título.</summary>
internal sealed record Problema(int Status, string Tipo, string Titulo)
{
    public ProblemDetails Detalhar(string? detalhe) => new() { Status = Status, Type = Tipo, Title = Titulo, Detail = detalhe };
}

/// <summary>
/// O catálogo dos problemas, no formato do RFC 9457 (ADR-0014). Cada tipo
/// aponta para a sua explicação em <c>docs/api/problemas.md</c>; o título é
/// fixo por tipo, e o detalhe diz o que aconteceu naquele pedido.
/// </summary>
internal static class Problemas
{
    private const string Documentacao = "https://github.com/MarcusBomfim/crm-obras/blob/main/docs/api/problemas.md#";

    public static readonly Problema PedidoInvalido = new(400, Documentacao + "pedido-invalido", "O pedido não está no formato esperado.");

    public static readonly Problema NaoAutenticado = new(401, Documentacao + "nao-autenticado", "É preciso se autenticar.");

    public static readonly Problema SemPermissao = new(403, Documentacao + "sem-permissao", "Sem permissão para este pedido.");

    public static readonly Problema NaoEncontrado = new(404, Documentacao + "nao-encontrado", "Não encontrado.");

    public static readonly Problema MetodoNaoPermitido = new(405, Documentacao + "metodo-nao-permitido", "Este recurso não aceita este método.");

    public static readonly Problema JaExiste = new(409, Documentacao + "ja-existe", "Já existe.");

    public static readonly Problema EmUso = new(409, Documentacao + "em-uso", "Em uso por outro registro.");

    public static readonly Problema VersaoDesatualizada = new(412, Documentacao + "versao-desatualizada", "A versão informada não é mais a atual.");

    public static readonly Problema TipoNaoSuportado = new(415, Documentacao + "tipo-nao-suportado", "O corpo do pedido precisa ser JSON.");

    public static readonly Problema RegraDoNegocio = new(422, Documentacao + "regra-do-negocio", "O pedido fere uma regra do negócio.");

    public static readonly Problema ChaveReutilizada = new(422, Documentacao + "chave-reutilizada", "A chave de idempotência já foi usada em outro pedido.");

    public static readonly Problema VersaoAusente = new(428, Documentacao + "versao-ausente", "Falta a versão lida, no cabeçalho If-Match.");

    public static readonly Problema ErroInterno = new(500, Documentacao + "erro-interno", "Erro interno.");

    /// <summary>Os problemas que o próprio ASP.NET produz sem exceção — 401, 403, 404, 405 —, pelo status.</summary>
    private static readonly Dictionary<int, Problema> DoFramework = new[]
    {
        NaoAutenticado, SemPermissao, NaoEncontrado, MetodoNaoPermitido, TipoNaoSuportado,
    }.ToDictionary(problema => problema.Status);

    /// <summary>
    /// Completa todo ProblemDetails que sai da API: o pedido, em
    /// <c>instance</c>, e — nos que o framework produz sozinho — o tipo e o
    /// título do catálogo, em português. O id do rastreio, para achar o
    /// pedido nos registros, o próprio ASP.NET já põe em todos.
    /// </summary>
    public static void Completar(ProblemDetailsContext contexto)
    {
        var detalhes = contexto.ProblemDetails;

        if (detalhes.Status is { } status
            && DoFramework.TryGetValue(status, out var problema)
            && detalhes.Type?.StartsWith(Documentacao, StringComparison.Ordinal) != true)
        {
            detalhes.Type = problema.Tipo;
            detalhes.Title = problema.Titulo;
        }

        detalhes.Instance ??= $"{contexto.HttpContext.Request.Method} {contexto.HttpContext.Request.Path}";
    }
}
