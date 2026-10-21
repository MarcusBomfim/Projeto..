using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Relatorios;

/// <summary>Quantas vendas chegaram a uma etapa, e o que aconteceu com elas.</summary>
/// <param name="Etapa">A etapa.</param>
/// <param name="Chegaram">As que chegaram à etapa, ou a uma depois dela.</param>
/// <param name="Avancaram">As que foram além da etapa: a uma depois dela, ou ao ganho.</param>
/// <param name="Ganhas">Das que chegaram, as ganhas.</param>
/// <param name="Perdidas">Das que chegaram, as perdidas.</param>
public sealed record ConversaoNaEtapa(Etapa Etapa, int Chegaram, int Avancaram, int Ganhas, int Perdidas)
{
    /// <summary>Das que chegaram, as ainda abertas.</summary>
    public int EmAndamento => Chegaram - Ganhas - Perdidas;

    /// <summary>
    /// Das que chegaram e já fecharam, quantas foram ganhas, em porcentagem
    /// com uma casa; nula se nenhuma fechou. É a probabilidade da etapa
    /// medida, para comparar com a configurada.
    /// </summary>
    public decimal? TaxaDeGanho => Ganhas + Perdidas == 0 ? null : decimal.Round(100m * Ganhas / (Ganhas + Perdidas), 1);
}

/// <summary>
/// Das vendas que nasceram no período, quantas chegaram a cada etapa do
/// funil, quantas foram além, e quantas foram ganhas (ADR-0009).
/// </summary>
/// <remarks>
/// <para>
/// Chegar a uma etapa é alcançá-la ou alcançar uma depois dela: a venda que
/// pulou a visita técnica e foi direto para a proposta passou pela visita.
/// A ganha chegou a todas. A ordem é a do funil hoje, só com as etapas em
/// uso.
/// </para>
/// <para>
/// A taxa de ganho conta só as que já fecharam: a venda aberta ainda não
/// ganhou nem perdeu, e contá-la como perdida puxaria a taxa para baixo.
/// </para>
/// </remarks>
public sealed class ConversaoPorEtapa
{
    private ConversaoPorEtapa(Periodo periodo, int vendas, IReadOnlyList<ConversaoNaEtapa> etapas)
    {
        Periodo = periodo;
        Vendas = vendas;
        Etapas = etapas;
    }

    public Periodo Periodo { get; }

    /// <summary>Quantas vendas do funil nasceram no período.</summary>
    public int Vendas { get; }

    public IReadOnlyList<ConversaoNaEtapa> Etapas { get; }

    /// <param name="funil">O funil.</param>
    /// <param name="oportunidades">As vendas; entram as do funil que nasceram no período.</param>
    /// <param name="periodo">Os dias em que as vendas nasceram.</param>
    public static ConversaoPorEtapa Do(Funil funil, IEnumerable<Oportunidade> oportunidades, Periodo periodo)
    {
        ArgumentNullException.ThrowIfNull(funil);
        ArgumentNullException.ThrowIfNull(oportunidades);
        ArgumentNullException.ThrowIfNull(periodo);

        var vendas = oportunidades
            .Where(oportunidade => oportunidade.Funil == funil.Id && periodo.Contem(oportunidade.CriadaEm))
            .Select(oportunidade => (oportunidade.Situacao, Alcance: Alcance(oportunidade, funil)))
            .ToList();

        return new ConversaoPorEtapa(
            periodo,
            vendas.Count,
            [
                .. funil.Abertas.Select((etapa, posicao) =>
                {
                    var chegaram = vendas.Where(venda => venda.Alcance >= posicao).ToList();

                    return new ConversaoNaEtapa(
                        etapa,
                        chegaram.Count,
                        chegaram.Count(venda => venda.Alcance > posicao),
                        chegaram.Count(venda => venda.Situacao == SituacaoDaOportunidade.Ganha),
                        chegaram.Count(venda => venda.Situacao == SituacaoDaOportunidade.Perdida));
                }),
            ]);
    }

    /// <summary>A posição da etapa aberta mais adiante por onde a venda passou; a ganha passou de todas.</summary>
    private static int Alcance(Oportunidade venda, Funil funil) =>
        venda.Situacao == SituacaoDaOportunidade.Ganha
            ? funil.Abertas.Count
            : venda.Passagens.Max(passagem => IndiceDa(funil, passagem.Etapa));

    private static int IndiceDa(Funil funil, EtapaId etapa)
    {
        for (var posicao = 0; posicao < funil.Abertas.Count; posicao++)
        {
            if (funil.Abertas[posicao].Id == etapa)
            {
                return posicao;
            }
        }

        return -1;
    }
}
