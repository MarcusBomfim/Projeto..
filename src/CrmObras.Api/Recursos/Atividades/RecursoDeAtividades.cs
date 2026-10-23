using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Atividades;

/// <summary>O que se faz com o cliente — e a agenda de cada vendedor (ADR-0006).</summary>
public sealed class RecursoDeAtividades : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var atividades = api.MapGroup("/atividades").WithTags("Atividades");

        atividades.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        atividades.MapPost("/", Criar).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        atividades.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        atividades.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        atividades.MapPut("/{id:guid}/data", Reagendar).RequireAuthorization(Politicas.Vender);
        atividades.MapPut("/{id:guid}/responsavel", Transferir).RequireAuthorization(Politicas.Vender);
        atividades.MapPost("/{id:guid}/conclusao", Concluir).RequireAuthorization(Politicas.Vender);
        atividades.MapPost("/{id:guid}/cancelamento", Cancelar).RequireAuthorization(Politicas.Vender);

        api.MapGet("/agenda", Agenda).WithTags("Atividades").RequireAuthorization(Politicas.Consultar);
    }

    /// <summary>As atividades de uma conta, pela data: <c>?conta=</c>.</summary>
    private static async Task<Ok<IReadOnlyList<ResumoDaAtividadeResposta>>> Listar(
        Guid conta,
        RepositorioDeAtividades repositorio,
        CancellationToken cancelamento)
    {
        var lidas = await repositorio.DaConta(new ContaId(conta), cancelamento);

        return TypedResults.Ok<IReadOnlyList<ResumoDaAtividadeResposta>>([.. lidas.Select(ResumoDaAtividadeResposta.De)]);
    }

    private static async Task<Created<AtividadeResposta>> Criar(
        NovaAtividade nova,
        RepositorioDeAtividades repositorio,
        RepositorioDeContas contas,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var conta = (await Leituras.Conta(contas, nova.Conta, cancelamento)).Agregado;
        var venda = nova.Oportunidade is { } daVenda ? (await Leituras.Oportunidade(oportunidades, daVenda, cancelamento)).Agregado : null;
        var agora = fuso.Agora();

        var atividade = new Atividade(
            conta,
            new DadosDaAtividade(nova.Tipo, nova.Assunto, nova.PrevistaPara, nova.Com is { } com ? new ContatoId(com) : null, nova.Detalhes),
            QuemPede.Login(http.User),
            agora,
            venda);

        Etiquetas.Escrever(http, await repositorio.Adicionar(atividade, cancelamento));

        return TypedResults.Created($"/api/atividades/{atividade.Id}", AtividadeResposta.De(atividade, agora));
    }

    private static async Task<Results<Ok<AtividadeResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Atividade(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(AtividadeResposta.De(lida.Agregado, fuso.Agora()));
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeAtividades repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new AtividadeId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    private static Task<Ok<AtividadeResposta>> Reagendar(
        Guid id,
        Reagendamento reagendamento,
        VersaoLida versao,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (atividade, agora) => atividade.Reagendar(reagendamento.Para, agora), cancelamento);

    /// <summary>Passa a atividade a outra pessoa: a visita técnica vai para o engenheiro.</summary>
    private static Task<Ok<AtividadeResposta>> Transferir(
        Guid id,
        NovoResponsavel novo,
        VersaoLida versao,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (atividade, _) => atividade.TransferirPara(novo.Responsavel), cancelamento);

    private static Task<Ok<AtividadeResposta>> Concluir(
        Guid id,
        Conclusao conclusao,
        VersaoLida versao,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (atividade, agora) => atividade.Concluir(conclusao.Resultado, agora), cancelamento);

    private static Task<Ok<AtividadeResposta>> Cancelar(
        Guid id,
        Cancelamento cancelamentoDaAtividade,
        VersaoLida versao,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(id, versao, repositorio, fuso, http, (atividade, agora) => atividade.Cancelar(cancelamentoDaAtividade.Motivo, agora), cancelamento);

    /// <summary>
    /// A agenda de um vendedor — de quem pede, sem <c>?responsavel=</c> —, no
    /// fuso da construtora ou no que vier em <c>?fuso=</c>.
    /// </summary>
    private static async Task<Ok<AgendaResposta>> Agenda(
        string? responsavel,
        string? fuso,
        RepositorioDeAtividades atividades,
        RepositorioDeOportunidades oportunidades,
        RepositorioDeContas contas,
        Fuso daConstrutora,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var de = responsavel ?? QuemPede.Login(http.User);
        var agora = fuso is null ? daConstrutora.Agora() : daConstrutora.AgoraEm(fuso.ComoFuso(nameof(fuso)));

        var vendas = await oportunidades.AbertasDoResponsavel(de, cancelamento);
        var pendentes = await atividades.PendentesParaAAgenda(de, vendas.Select(venda => venda.Agregado.Id), cancelamento);
        var versoes = pendentes.ToDictionary(lida => lida.Agregado.Id, lida => lida.Versao);
        var versoesDasVendas = vendas.ToDictionary(lida => lida.Agregado.Id, lida => lida.Versao);
        var nomes = (await contas.Listar(cancelamento)).ToDictionary(resumo => resumo.Id, resumo => resumo.Nome);

        var agenda = Dominio.Atividades.Agenda.De(de, pendentes.Select(lida => lida.Agregado), vendas.Select(lida => lida.Agregado), agora);

        AtividadeNaAgendaResposta NaAgenda(Atividade atividade) => new(
            atividade.Id.Valor,
            atividade.Tipo,
            atividade.Assunto,
            atividade.PrevistaPara,
            atividade.Conta.Valor,
            nomes[atividade.Conta],
            atividade.Oportunidade?.Valor,
            atividade.Adiamentos,
            versoes[atividade.Id]);

        return TypedResults.Ok(new AgendaResposta(
            agenda.Dia,
            de,
            [.. agenda.Atrasadas.Select(NaAgenda)],
            [.. agenda.DeHoje.Select(NaAgenda)],
            [.. agenda.SemProximoPasso.Select(venda => VendaSemProximoPassoResposta.De(venda, nomes[venda.Conta], versoesDasVendas[venda.Id]))]));
    }

    private static async Task<Ok<AtividadeResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeAtividades repositorio,
        Fuso fuso,
        HttpContext http,
        Action<Atividade, DateTimeOffset> alteracao,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Atividade(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A atividade {id}");

        var agora = fuso.Agora();
        alteracao(lida.Agregado, agora);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lida.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(AtividadeResposta.De(lida.Agregado, agora));
    }
}
