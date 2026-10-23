using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Clientes;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Contas;

/// <summary>Os clientes da construtora e as pessoas com quem se fala em cada um.</summary>
public sealed class RecursoDeContas : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var contas = api.MapGroup("/contas").WithTags("Contas");

        contas.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        contas.MapPost("/", Criar).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        contas.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        contas.MapPatch("/{id:guid}", AlterarConta).RequireAuthorization(Politicas.Vender);
        contas.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        contas.MapPost("/{id:guid}/contatos", AdicionarContato).RequireAuthorization(Politicas.Vender);
        contas.MapPut("/{id:guid}/contatos/{contato:guid}", AlterarContato).RequireAuthorization(Politicas.Vender);
        contas.MapDelete("/{id:guid}/contatos/{contato:guid}", RemoverContato).RequireAuthorization(Politicas.Vender);
        contas.MapPut("/{id:guid}/principal", DefinirPrincipal).RequireAuthorization(Politicas.Vender);
    }

    private static async Task<Ok<IReadOnlyList<ResumoResposta>>> Listar(RepositorioDeContas repositorio, CancellationToken cancelamento)
    {
        var resumos = await repositorio.Listar(cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoResposta>>([.. resumos.Select(ResumoResposta.De)]);
    }

    private static async Task<Created<ContaResposta>> Criar(
        NovaConta nova,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var conta = new Conta(nova.Nome, nova.Documento.ComoDocumento());

        foreach (var contato in nova.Contatos ?? [])
        {
            conta.AdicionarContato(contato.ParaDados());
        }

        Etiquetas.Escrever(http, await repositorio.Adicionar(conta, cancelamento));

        return TypedResults.Created($"/api/contas/{conta.Id}", ContaResposta.De(conta));
    }

    private static async Task<Results<Ok<ContaResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Conta(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(ContaResposta.De(lida.Agregado));
    }

    private static Task<Ok<ContaResposta>> AlterarConta(
        Guid id,
        AlteracaoDaConta alteracao,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        if (alteracao is { Nome: null, Documento: null })
        {
            throw new PedidoInvalido("Diga o que muda na conta: nome, documento, ou os dois.");
        }

        return Alterar(
            id,
            versao,
            repositorio,
            http,
            conta =>
            {
                if (alteracao.Nome is { } nome)
                {
                    conta.Renomear(nome);
                }

                if (alteracao.Documento.ComoDocumento() is { } documento)
                {
                    conta.InformarDocumento(documento);
                }
            },
            cancelamento);
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeContas repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new ContaId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<ContaResposta>> AdicionarContato(
        Guid id,
        NovoContato novo,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, conta => conta.AdicionarContato(novo.ParaDados()), cancelamento);

    private static Task<Ok<ContaResposta>> AlterarContato(
        Guid id,
        Guid contato,
        NovoContato novo,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            conta => conta.AlterarContato(Exigir(conta, contato), novo.ParaDados()),
            cancelamento);

    private static Task<Ok<ContaResposta>> RemoverContato(
        Guid id,
        Guid contato,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, conta => conta.RemoverContato(Exigir(conta, contato)), cancelamento);

    private static Task<Ok<ContaResposta>> DefinirPrincipal(
        Guid id,
        NovoPrincipal novo,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, http, conta => conta.DefinirPrincipal(new ContatoId(novo.Contato)), cancelamento);

    private static async Task<Ok<ContaResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeContas repositorio,
        HttpContext http,
        Action<Conta> alteracao,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Conta(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A conta {id}");

        alteracao(lida.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lida.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(ContaResposta.De(lida.Agregado));
    }

    /// <summary>
    /// O contato que o caminho aponta — ou 404: não é a regra que recusa, é o
    /// endereço que não existe. O contato que vem no corpo, a conta confere.
    /// </summary>
    private static ContatoId Exigir(Conta conta, Guid contato)
    {
        var procurado = new ContatoId(contato);

        return conta.Contatos.Any(existente => existente.Id == procurado)
            ? procurado
            : throw new AgregadoNaoEncontrado($"A conta {conta.Id} não tem o contato {contato}.");
    }
}
