using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Relatorios;

/// <summary>Quanto tempo as vendas ganhas passaram numa etapa.</summary>
/// <param name="Etapa">A etapa.</param>
/// <param name="Vendas">Quantas das ganhas passaram por ela.</param>
/// <param name="Mediana">O tempo típico na etapa, entre as que passaram por ela; nula se nenhuma passou.</param>
public sealed record TempoTipicoNaEtapa(Etapa Etapa, int Vendas, TimeSpan? Mediana);

/// <summary>
/// Quanto tempo leva para vender: da abertura ao ganho, e em cada etapa,
/// para as vendas ganhas no período (ADR-0009).
/// </summary>
/// <remarks>
/// O tempo típico é a mediana, e não a média: uma venda de obra pública que
/// levou dois anos puxa a média para cima, e a mediana nem percebe. A média
/// vem junto, e a distância entre as duas diz quanto os casos fora da curva
/// pesam.
/// </remarks>
public sealed class CicloDeVenda
{
    private CicloDeVenda(Periodo periodo, IReadOnlyList<TimeSpan> ciclos, IReadOnlyList<TempoTipicoNaEtapa> etapas)
    {
        Periodo = periodo;
        Ganhas = ciclos.Count;
        Mediana = MedianaDe(ciclos);
        Media = ciclos.Count == 0 ? null : TimeSpan.FromTicks((long)ciclos.Average(ciclo => ciclo.Ticks));
        MaisCurto = ciclos.Count == 0 ? null : ciclos.Min();
        MaisLongo = ciclos.Count == 0 ? null : ciclos.Max();
        Etapas = etapas;
    }

    public Periodo Periodo { get; }

    /// <summary>Quantas vendas do funil foram ganhas no período.</summary>
    public int Ganhas { get; }

    /// <summary>O tempo típico da abertura ao ganho; nulo se nenhuma venda foi ganha.</summary>
    public TimeSpan? Mediana { get; }

    public TimeSpan? Media { get; }

    public TimeSpan? MaisCurto { get; }

    public TimeSpan? MaisLongo { get; }

    /// <summary>O tempo típico em cada etapa em uso do funil, na ordem da venda.</summary>
    public IReadOnlyList<TempoTipicoNaEtapa> Etapas { get; }

    /// <param name="funil">O funil.</param>
    /// <param name="oportunidades">As vendas; entram as do funil ganhas no período.</param>
    /// <param name="periodo">Os dias em que as vendas foram ganhas.</param>
    public static CicloDeVenda Do(Funil funil, IEnumerable<Oportunidade> oportunidades, Periodo periodo)
    {
        ArgumentNullException.ThrowIfNull(funil);
        ArgumentNullException.ThrowIfNull(oportunidades);
        ArgumentNullException.ThrowIfNull(periodo);

        var ganhas = oportunidades
            .Where(oportunidade => oportunidade.Funil == funil.Id
                && oportunidade is { Situacao: SituacaoDaOportunidade.Ganha, FechadaEm: { } fechadaEm }
                && periodo.Contem(fechadaEm))
            .ToList();
        var tempos = ganhas.SelectMany(ganha => ganha.TempoEmCadaEtapa(ganha.FechadaEm!.Value)).ToList();

        return new CicloDeVenda(
            periodo,
            [.. ganhas.Select(ganha => ganha.FechadaEm!.Value - ganha.CriadaEm)],
            [
                .. funil.Abertas.Select(etapa =>
                {
                    List<TimeSpan> naEtapa = [.. tempos.Where(tempo => tempo.Etapa == etapa.Id).Select(tempo => tempo.Tempo)];

                    return new TempoTipicoNaEtapa(etapa, naEtapa.Count, MedianaDe(naEtapa));
                }),
            ]);
    }

    /// <summary>O do meio, com os tempos em ordem; havendo dois no meio, a média deles.</summary>
    private static TimeSpan? MedianaDe(IReadOnlyList<TimeSpan> tempos)
    {
        if (tempos.Count == 0)
        {
            return null;
        }

        List<TimeSpan> ordem = [.. tempos.Order()];
        var meio = ordem.Count / 2;

        return ordem.Count % 2 == 1 ? ordem[meio] : (ordem[meio - 1] + ordem[meio]) / 2;
    }
}
