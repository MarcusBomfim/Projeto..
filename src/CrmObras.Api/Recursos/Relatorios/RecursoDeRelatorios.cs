using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Relatorios;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Relatorios;

/// <summary>
/// A previsão de receita e os relatórios do funil. Não guardam nada: saem do
/// histórico das vendas a cada pedido, e qualquer período passado pode ser
/// refeito. Os dias são os do fuso da construtora.
/// </summary>
public sealed class RecursoDeRelatorios : IRecursoDaApi
{
    private const int MesesDaPrevisao = 3;

    public void Mapear(IEndpointRouteBuilder api)
    {
        var relatorios = api.MapGroup("/relatorios").WithTags("Relatórios").RequireAuthorization(Politicas.Consultar);

        relatorios.MapGet("/previsao", Previsao);
        relatorios.MapGet("/conversao", Conversao);
        relatorios.MapGet("/ciclo", Ciclo);
        relatorios.MapGet("/perdas", Perdas);
    }

    /// <summary>
    /// A previsão das vendas de todos os funis, do mês <c>?primeiro=2027-01</c>
    /// — o de hoje, sem ele — por <c>?meses=</c> meses — três, sem ele.
    /// </summary>
    private static async Task<Ok<PrevisaoResposta>> Previsao(
        string? primeiro,
        int? meses,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var agora = fuso.Agora();
        var inicio = primeiro?.ComoMes(nameof(primeiro)) ?? Mes.De(DateOnly.FromDateTime(agora.DateTime));

        List<Funil> todos = [];
        List<Oportunidade> vendas = [];

        foreach (var resumo in await funis.Listar(cancelamento))
        {
            var funil = (await Leituras.Funil(funis, resumo.Id.Valor, cancelamento)).Agregado;
            todos.Add(funil);
            vendas.AddRange((await oportunidades.DoFunil(funil.Id, cancelamento)).Select(lida => lida.Agregado));
        }

        return TypedResults.Ok(PrevisaoResposta.De(PrevisaoDeReceita.Do(todos, vendas, inicio, meses ?? MesesDaPrevisao, agora)));
    }

    /// <summary>Das vendas do <c>?funil=</c> que nasceram de <c>?de=</c> a <c>?ate=</c>, quantas chegaram a cada etapa.</summary>
    private static async Task<Ok<ConversaoPorEtapaResposta>> Conversao(
        Guid funil,
        string de,
        string ate,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var (doFunil, vendas, periodo) = await Ler(funil, de, ate, funis, oportunidades, fuso, cancelamento);

        return TypedResults.Ok(ConversaoPorEtapaResposta.De(ConversaoPorEtapa.Do(doFunil, vendas, periodo)));
    }

    /// <summary>Quanto tempo levaram as vendas do <c>?funil=</c> ganhas de <c>?de=</c> a <c>?ate=</c>.</summary>
    private static async Task<Ok<CicloDeVendaResposta>> Ciclo(
        Guid funil,
        string de,
        string ate,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var (doFunil, vendas, periodo) = await Ler(funil, de, ate, funis, oportunidades, fuso, cancelamento);

        return TypedResults.Ok(CicloDeVendaResposta.De(CicloDeVenda.Do(doFunil, vendas, periodo)));
    }

    /// <summary>Por que se perderam as vendas do <c>?funil=</c> perdidas de <c>?de=</c> a <c>?ate=</c>.</summary>
    private static async Task<Ok<MotivosDePerdaResposta>> Perdas(
        Guid funil,
        string de,
        string ate,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var (doFunil, vendas, periodo) = await Ler(funil, de, ate, funis, oportunidades, fuso, cancelamento);

        return TypedResults.Ok(MotivosDePerdaResposta.De(MotivosDePerda.Do(doFunil, vendas, periodo)));
    }

    /// <summary>O funil, as vendas dele e o período, nos dias do fuso da construtora.</summary>
    private static async Task<(Funil Funil, IReadOnlyList<Oportunidade> Vendas, Periodo Periodo)> Ler(
        Guid id,
        string de,
        string ate,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var periodo = Periodo.De(de.ComoDia(nameof(de)), ate.ComoDia(nameof(ate)), fuso.Agora().Offset);
        var funil = (await Leituras.Funil(funis, id, cancelamento)).Agregado;
        var vendas = await oportunidades.DoFunil(funil.Id, cancelamento);

        return (funil, [.. vendas.Select(lida => lida.Agregado)], periodo);
    }
}
