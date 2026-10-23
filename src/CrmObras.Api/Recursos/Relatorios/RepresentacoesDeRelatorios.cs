using System.Globalization;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Relatorios;

namespace CrmObras.Api.Recursos.Relatorios;

/// <summary>
/// A receita prevista mês a mês, e à parte o que não entra na previsão: a
/// venda vencida, a sem valor e a sem data (ADR-0008).
/// </summary>
public sealed record PrevisaoResposta(
    IReadOnlyList<MesDaPrevisaoResposta> Meses,
    decimal Ganho,
    decimal Ponderado,
    decimal Previsto,
    IReadOnlyList<VendaNoRelatorioResposta> Vencidas,
    IReadOnlyList<VendaNoRelatorioResposta> SemValor,
    IReadOnlyList<VendaNoRelatorioResposta> SemPrevisao)
{
    public static PrevisaoResposta De(PrevisaoDeReceita previsao)
    {
        ArgumentNullException.ThrowIfNull(previsao);

        return new(
            [
                .. previsao.Meses.Select(mes => new MesDaPrevisaoResposta(
                    string.Create(CultureInfo.InvariantCulture, $"{mes.Mes.Ano:D4}-{mes.Mes.Numero:D2}"),
                    mes.Ganho.Valor,
                    mes.EmAberto.Valor,
                    mes.Ponderado.Valor,
                    mes.Previsto.Valor,
                    [.. mes.Abertas.Select(VendaNoRelatorioResposta.De)])),
            ],
            previsao.Ganho.Valor,
            previsao.Ponderado.Valor,
            previsao.Previsto.Valor,
            [.. previsao.Vencidas.Select(VendaNoRelatorioResposta.De)],
            [.. previsao.SemValor.Select(VendaNoRelatorioResposta.De)],
            [.. previsao.SemPrevisao.Select(VendaNoRelatorioResposta.De)]);
    }
}

/// <summary>Um mês: o já ganho nele, o em aberto, o ponderado pela etapa, e as vendas abertas que devem fechar nele.</summary>
/// <param name="Mes">Como <c>2027-01</c>.</param>
public sealed record MesDaPrevisaoResposta(
    string Mes,
    decimal Ganho,
    decimal EmAberto,
    decimal Ponderado,
    decimal Previsto,
    IReadOnlyList<VendaNoRelatorioResposta> Abertas);

public sealed record VendaNoRelatorioResposta(
    Guid Id,
    string Titulo,
    Guid Etapa,
    string Responsavel,
    decimal? ValorEstimado,
    DateOnly? PrevisaoDeFechamento)
{
    public static VendaNoRelatorioResposta De(Oportunidade venda)
    {
        ArgumentNullException.ThrowIfNull(venda);

        return new(venda.Id.Valor, venda.Titulo, venda.Etapa.Valor, venda.Responsavel, venda.ValorEstimado?.Valor, venda.PrevisaoDeFechamento);
    }
}

/// <summary>Das vendas que nasceram no período, quantas chegaram a cada etapa e o que houve com elas (ADR-0009).</summary>
public sealed record ConversaoPorEtapaResposta(DateOnly PrimeiroDia, DateOnly UltimoDia, int Vendas, IReadOnlyList<ConversaoNaEtapaResposta> Etapas)
{
    public static ConversaoPorEtapaResposta De(ConversaoPorEtapa conversao)
    {
        ArgumentNullException.ThrowIfNull(conversao);

        return new(
            conversao.Periodo.PrimeiroDia,
            conversao.Periodo.UltimoDia,
            conversao.Vendas,
            [
                .. conversao.Etapas.Select(etapa => new ConversaoNaEtapaResposta(
                    etapa.Etapa.Id.Valor,
                    etapa.Etapa.Nome,
                    etapa.Etapa.Probabilidade,
                    etapa.Chegaram,
                    etapa.Avancaram,
                    etapa.Ganhas,
                    etapa.Perdidas,
                    etapa.EmAndamento,
                    etapa.TaxaDeGanho)),
            ]);
    }
}

/// <summary>Uma etapa da conversão. A taxa de ganho conta só as que já fecharam; nula se nenhuma fechou.</summary>
public sealed record ConversaoNaEtapaResposta(
    Guid Etapa,
    string Nome,
    int Probabilidade,
    int Chegaram,
    int Avancaram,
    int Ganhas,
    int Perdidas,
    int EmAndamento,
    decimal? TaxaDeGanho);

/// <summary>
/// Quanto tempo leva para vender, das ganhas no período, em dias com uma
/// casa: da abertura ao ganho, e em cada etapa (ADR-0009).
/// </summary>
public sealed record CicloDeVendaResposta(
    DateOnly PrimeiroDia,
    DateOnly UltimoDia,
    int Ganhas,
    decimal? MedianaEmDias,
    decimal? MediaEmDias,
    decimal? MaisCurtoEmDias,
    decimal? MaisLongoEmDias,
    IReadOnlyList<TempoNaEtapaResposta> Etapas)
{
    public static CicloDeVendaResposta De(CicloDeVenda ciclo)
    {
        ArgumentNullException.ThrowIfNull(ciclo);

        return new(
            ciclo.Periodo.PrimeiroDia,
            ciclo.Periodo.UltimoDia,
            ciclo.Ganhas,
            EmDias(ciclo.Mediana),
            EmDias(ciclo.Media),
            EmDias(ciclo.MaisCurto),
            EmDias(ciclo.MaisLongo),
            [.. ciclo.Etapas.Select(etapa => new TempoNaEtapaResposta(etapa.Etapa.Id.Valor, etapa.Etapa.Nome, etapa.Vendas, EmDias(etapa.Mediana)))]);
    }

    private static decimal? EmDias(TimeSpan? tempo) => tempo is { } duracao ? decimal.Round((decimal)duracao.TotalDays, 1) : null;
}

/// <summary>O tempo típico na etapa, entre as ganhas que passaram por ela; nulo se nenhuma passou.</summary>
public sealed record TempoNaEtapaResposta(Guid Etapa, string Nome, int Vendas, decimal? MedianaEmDias);

/// <summary>As perdas do período: por motivo, por concorrente e pela etapa em que a venda estava.</summary>
public sealed record MotivosDePerdaResposta(
    DateOnly PrimeiroDia,
    DateOnly UltimoDia,
    int Perdidas,
    decimal Valor,
    int SemValor,
    IReadOnlyList<PerdasPorMotivoResposta> PorMotivo,
    IReadOnlyList<PerdasParaConcorrenteResposta> PorConcorrente,
    IReadOnlyList<PerdasNaEtapaResposta> PorEtapa)
{
    public static MotivosDePerdaResposta De(MotivosDePerda perdas)
    {
        ArgumentNullException.ThrowIfNull(perdas);

        return new(
            perdas.Periodo.PrimeiroDia,
            perdas.Periodo.UltimoDia,
            perdas.Perdidas,
            perdas.Valor.Valor,
            perdas.SemValor,
            [.. perdas.PorMotivo.Select(motivo => new PerdasPorMotivoResposta(motivo.Motivo, motivo.Vendas, motivo.Valor.Valor))],
            [.. perdas.PorConcorrente.Select(concorrente => new PerdasParaConcorrenteResposta(concorrente.Concorrente, concorrente.Vendas, concorrente.Valor.Valor))],
            [.. perdas.PorEtapa.Select(etapa => new PerdasNaEtapaResposta(etapa.Etapa.Id.Valor, etapa.Etapa.Nome, etapa.Vendas, etapa.Valor.Valor))]);
    }
}

public sealed record PerdasPorMotivoResposta(MotivoDaPerda Motivo, int Vendas, decimal Valor);

public sealed record PerdasParaConcorrenteResposta(string Concorrente, int Vendas, decimal Valor);

public sealed record PerdasNaEtapaResposta(Guid Etapa, string Nome, int Vendas, decimal Valor);
