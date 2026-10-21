using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Relatorios;

/// <summary>Quantas vendas se perderam por um motivo, e quanto elas valiam.</summary>
public sealed record PerdasPorMotivo(MotivoDaPerda Motivo, int Vendas, Dinheiro Valor);

/// <summary>Quantas vendas foram para um concorrente, e quanto elas valiam.</summary>
/// <param name="Concorrente">O nome como foi escrito na primeira perda para ele.</param>
/// <param name="Vendas">Quantas vendas.</param>
/// <param name="Valor">A soma dos valores estimados.</param>
public sealed record PerdasParaConcorrente(string Concorrente, int Vendas, Dinheiro Valor);

/// <summary>Quantas vendas se perderam numa etapa, e quanto elas valiam.</summary>
public sealed record PerdasNaEtapa(Etapa Etapa, int Vendas, Dinheiro Valor);

/// <summary>
/// Onde e por que o comercial perde obra: as vendas perdidas no período, por
/// motivo, por concorrente e pela etapa em que estavam.
/// </summary>
/// <remarks>
/// <para>
/// Cada lista vem da mais frequente para a menos, e, no empate, da que mais
/// valia. O valor é o estimado: a venda perdida não tem valor fechado. A sem
/// valor estimado conta na quantidade, e não no valor — o relatório diz
/// quantas são.
/// </para>
/// <para>
/// O concorrente é comparado como o nome de uma empresa na busca de
/// duplicados — sem acento, maiúscula, pontuação e "Ltda" —: "Construtora
/// Beta Ltda." e "construtora beta" são o mesmo concorrente. Conta toda perda
/// que diz o concorrente, e não só a de motivo "concorrente": a perdida por
/// preço também pode dizer para quem.
/// </para>
/// <para>
/// A venda perdida e depois reaberta não está perdida, e não entra.
/// </para>
/// </remarks>
public sealed class MotivosDePerda
{
    private MotivosDePerda(
        Periodo periodo,
        IReadOnlyList<Oportunidade> perdidas,
        IReadOnlyList<PerdasPorMotivo> porMotivo,
        IReadOnlyList<PerdasParaConcorrente> porConcorrente,
        IReadOnlyList<PerdasNaEtapa> porEtapa)
    {
        Periodo = periodo;
        Perdidas = perdidas.Count;
        Valor = Somar(perdidas);
        SemValor = perdidas.Count(perdida => perdida.ValorEstimado is null);
        PorMotivo = porMotivo;
        PorConcorrente = porConcorrente;
        PorEtapa = porEtapa;
    }

    public Periodo Periodo { get; }

    /// <summary>Quantas vendas do funil foram perdidas no período.</summary>
    public int Perdidas { get; }

    /// <summary>A soma dos valores estimados das perdidas.</summary>
    public Dinheiro Valor { get; }

    /// <summary>Quantas perdidas não tinham valor estimado.</summary>
    public int SemValor { get; }

    public IReadOnlyList<PerdasPorMotivo> PorMotivo { get; }

    public IReadOnlyList<PerdasParaConcorrente> PorConcorrente { get; }

    /// <summary>Pela etapa em que a venda estava quando foi perdida, na ordem do funil; as arquivadas, no fim.</summary>
    public IReadOnlyList<PerdasNaEtapa> PorEtapa { get; }

    /// <param name="funil">O funil.</param>
    /// <param name="oportunidades">As vendas; entram as do funil perdidas no período.</param>
    /// <param name="periodo">Os dias em que as vendas foram perdidas.</param>
    public static MotivosDePerda Do(Funil funil, IEnumerable<Oportunidade> oportunidades, Periodo periodo)
    {
        ArgumentNullException.ThrowIfNull(funil);
        ArgumentNullException.ThrowIfNull(oportunidades);
        ArgumentNullException.ThrowIfNull(periodo);

        List<Oportunidade> perdidas =
        [
            .. oportunidades
                .Where(oportunidade => oportunidade.Funil == funil.Id
                    && oportunidade is { Situacao: SituacaoDaOportunidade.Perdida, FechadaEm: { } fechadaEm }
                    && periodo.Contem(fechadaEm))
                .OrderBy(perdida => perdida.FechadaEm),
        ];

        return new MotivosDePerda(
            periodo,
            perdidas,
            [
                .. Ordenar(perdidas.GroupBy(perdida => perdida.Perda!.Motivo))
                    .Select(grupo => new PerdasPorMotivo(grupo.Key, grupo.Count(), Somar(grupo))),
            ],
            [
                .. Ordenar(perdidas
                    .Where(perdida => perdida.Perda!.Concorrente is not null)
                    .GroupBy(perdida => BuscaDeDuplicados.NomeComparavel(perdida.Perda!.Concorrente!)))
                    .Select(grupo => new PerdasParaConcorrente(grupo.First().Perda!.Concorrente!, grupo.Count(), Somar(grupo))),
            ],
            [
                .. perdidas
                    .GroupBy(perdida => perdida.Passagens[^2].Etapa)
                    .Select(grupo => new PerdasNaEtapa(funil.Etapa(grupo.Key), grupo.Count(), Somar(grupo)))
                    .OrderBy(perdas => perdas.Etapa.Arquivada)
                    .ThenBy(perdas => Posicao(funil, perdas.Etapa)),
            ]);
    }

    private static IOrderedEnumerable<IGrouping<TChave, Oportunidade>> Ordenar<TChave>(IEnumerable<IGrouping<TChave, Oportunidade>> grupos) =>
        grupos.OrderByDescending(grupo => grupo.Count()).ThenByDescending(Somar);

    private static Dinheiro Somar(IEnumerable<Oportunidade> perdidas) =>
        perdidas.Aggregate(Dinheiro.Zero, (soma, perdida) => soma + (perdida.ValorEstimado ?? Dinheiro.Zero));

    /// <summary>A posição entre as abertas em uso; para a arquivada, a ordem em que saiu do funil.</summary>
    private static int Posicao(Funil funil, Etapa etapa) =>
        etapa.Arquivada ? IndiceDa(funil.Arquivadas, etapa) : IndiceDa(funil.Abertas, etapa);

    private static int IndiceDa(IReadOnlyList<Etapa> etapas, Etapa etapa)
    {
        for (var posicao = 0; posicao < etapas.Count; posicao++)
        {
            if (etapas[posicao] == etapa)
            {
                return posicao;
            }
        }

        return -1;
    }
}
