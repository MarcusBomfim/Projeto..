using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Funis;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Funis;

/// <summary>Os caminhos da venda e as etapas de cada um (ADR-0004). Ler é de todos; configurar, da gerência.</summary>
public sealed class RecursoDeFunis : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var funis = api.MapGroup("/funis").WithTags("Funis");

        funis.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        funis.MapPost("/", Criar).RequireAuthorization(Politicas.Gerir).ComIdempotencia();
        funis.MapPost("/padrao", CriarPadrao).RequireAuthorization(Politicas.Gerir).ComIdempotencia();
        funis.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        funis.MapPatch("/{id:guid}", Renomear).RequireAuthorization(Politicas.Gerir);
        funis.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        funis.MapPost("/{id:guid}/etapas", AdicionarEtapa).RequireAuthorization(Politicas.Gerir);
        funis.MapPut("/{id:guid}/etapas/{etapa:guid}", AlterarEtapa).RequireAuthorization(Politicas.Gerir);
        funis.MapPatch("/{id:guid}/etapas/{etapa:guid}", RenomearEtapa).RequireAuthorization(Politicas.Gerir);
        funis.MapPut("/{id:guid}/etapas/{etapa:guid}/posicao", MoverEtapa).RequireAuthorization(Politicas.Gerir);
        funis.MapPost("/{id:guid}/etapas/{etapa:guid}/arquivamento", ArquivarEtapa).RequireAuthorization(Politicas.Gerir);
        funis.MapPost("/{id:guid}/etapas/{etapa:guid}/reativacao", ReativarEtapa).RequireAuthorization(Politicas.Gerir);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeFunis repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    private static Task<Created<FunilResposta>> Criar(NovoFunil novo, RepositorioDeFunis repositorio, HttpContext http, CancellationToken cancelamento) =>
        Gravar(new Funil(novo.Nome, [.. novo.Etapas.Select(etapa => etapa.ParaDados())]), repositorio, http, cancelamento);

    /// <summary>O funil de quem vende obra a empresa, pronto: do primeiro contato à negociação.</summary>
    private static Task<Created<FunilResposta>> CriarPadrao(RepositorioDeFunis repositorio, HttpContext http, CancellationToken cancelamento) =>
        Gravar(Funil.PadraoDaConstrutora(), repositorio, http, cancelamento);

    private static async Task<Results<Ok<FunilResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Funil(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lido.Versao);

        return Etiquetas.ClienteJaTem(http, lido.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(FunilResposta.De(lido.Agregado));
    }

    private static Task<Ok<FunilResposta>> Renomear(
        Guid id,
        NovoNome novo,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.Renomear(novo.Nome), cancelamento);

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeFunis repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new FunilId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<FunilResposta>> AdicionarEtapa(
        Guid id,
        NovaEtapa nova,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.AdicionarEtapa(nova.ParaDados()), cancelamento);

    private static Task<Ok<FunilResposta>> AlterarEtapa(
        Guid id,
        Guid etapa,
        NovaEtapa nova,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.AlterarEtapa(Exigir(funil, etapa), nova.ParaDados()), cancelamento);

    private static Task<Ok<FunilResposta>> RenomearEtapa(
        Guid id,
        Guid etapa,
        NovoNome novo,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.RenomearEtapa(Exigir(funil, etapa), novo.Nome), cancelamento);

    private static Task<Ok<FunilResposta>> MoverEtapa(
        Guid id,
        Guid etapa,
        NovaPosicao nova,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.MoverEtapa(Exigir(funil, etapa), nova.Posicao), cancelamento);

    private static Task<Ok<FunilResposta>> ArquivarEtapa(
        Guid id,
        Guid etapa,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.ArquivarEtapa(Exigir(funil, etapa)), cancelamento);

    private static Task<Ok<FunilResposta>> ReativarEtapa(
        Guid id,
        Guid etapa,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, funil => funil.ReativarEtapa(Exigir(funil, etapa)), cancelamento);

    private static async Task<Created<FunilResposta>> Gravar(Funil funil, RepositorioDeFunis repositorio, HttpContext http, CancellationToken cancelamento)
    {
        Etiquetas.Escrever(http, await repositorio.Adicionar(funil, cancelamento));

        return TypedResults.Created($"/api/funis/{funil.Id}", FunilResposta.De(funil));
    }

    private static async Task<Ok<FunilResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeFunis repositorio,
        HttpContext http,
        Action<Funil> alteracao,
        CancellationToken cancelamento)
    {
        var lido = await Leituras.Funil(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lido, versao, $"O funil {id}");

        alteracao(lido.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lido.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(FunilResposta.De(lido.Agregado));
    }

    /// <summary>A etapa que o caminho aponta — a arquivada inclusive —, ou 404.</summary>
    private static EtapaId Exigir(Funil funil, Guid etapa)
    {
        var procurada = new EtapaId(etapa);

        return funil.Abertas.Concat(funil.Arquivadas).Append(funil.Ganha).Append(funil.Perdida).Any(existente => existente.Id == procurada)
            ? procurada
            : throw new AgregadoNaoEncontrado($"O funil {funil.Id} não tem a etapa {etapa}.");
    }
}
