using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;

namespace CrmObras.Dominio.Oportunidades;

/// <summary>
/// Uma coluna do quadro: a etapa e as vendas abertas nela, da que entrou há
/// mais tempo para a mais recente.
/// </summary>
/// <param name="Etapa">A etapa da coluna.</param>
/// <param name="Oportunidades">As vendas abertas na etapa.</param>
/// <param name="Paradas">As que estão na etapa há mais dias do que ela espera.</param>
public sealed record ColunaDoQuadro(Etapa Etapa, IReadOnlyList<Oportunidade> Oportunidades, IReadOnlyList<Oportunidade> Paradas)
{
    /// <summary>A soma dos valores estimados. Venda sem valor estimado conta como zero.</summary>
    public Dinheiro Total => Oportunidades.Aggregate(Dinheiro.Zero, (soma, oportunidade) => soma + (oportunidade.ValorEstimado ?? Dinheiro.Zero));

    /// <summary>O total multiplicado pela probabilidade da etapa, ao centavo: o que a coluna deve render.</summary>
    public Dinheiro Ponderado => (Total * (Etapa.Probabilidade / 100m)).EmCentavos();

    /// <summary>Quantas vendas da coluna ainda não têm valor estimado — e não entram no total.</summary>
    public int SemValor => Oportunidades.Count(oportunidade => oportunidade.ValorEstimado is null);
}

/// <summary>
/// O funil como o comercial olha para ele: uma coluna por etapa aberta, com as
/// vendas, quanto somam e quais estão paradas.
/// </summary>
/// <remarks>
/// <para>
/// Cada venda entra na coluna da etapa em que está, e por isso só as abertas
/// do funil aparecem: as de outros funis estão em etapas que não são deste, e
/// as ganhas e as perdidas, na Ganha e na Perdida, que não são colunas. As
/// etapas sem venda aparecem vazias — o quadro é o funil inteiro.
/// </para>
/// <para>
/// A etapa arquivada que ainda tem venda aparece no fim, depois das abertas:
/// arquivar não move ninguém, e a venda que ficou lá não pode sumir do quadro.
/// </para>
/// </remarks>
public sealed class QuadroDoFunil
{
    private QuadroDoFunil(IReadOnlyList<ColunaDoQuadro> colunas) => Colunas = colunas;

    public IReadOnlyList<ColunaDoQuadro> Colunas { get; }

    public Dinheiro Total => Colunas.Aggregate(Dinheiro.Zero, (soma, coluna) => soma + coluna.Total);

    /// <summary>A soma dos ponderados das colunas: o que o funil deve render.</summary>
    public Dinheiro Ponderado => Colunas.Aggregate(Dinheiro.Zero, (soma, coluna) => soma + coluna.Ponderado);

    public static QuadroDoFunil Do(Funil funil, IEnumerable<Oportunidade> oportunidades, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(funil);
        ArgumentNullException.ThrowIfNull(oportunidades);

        var porEtapa = oportunidades.ToLookup(oportunidade => oportunidade.Etapa);

        return new QuadroDoFunil(
        [
            .. funil.Abertas
                .Concat(funil.Arquivadas.Where(etapa => porEtapa.Contains(etapa.Id)))
                .Select(etapa => Coluna(funil, etapa, porEtapa[etapa.Id], agora)),
        ]);
    }

    private static ColunaDoQuadro Coluna(Funil funil, Etapa etapa, IEnumerable<Oportunidade> oportunidades, DateTimeOffset agora)
    {
        List<Oportunidade> naOrdem = [.. oportunidades.OrderBy(oportunidade => oportunidade.EntrouNaEtapaEm)];

        return new ColunaDoQuadro(etapa, naOrdem, [.. naOrdem.Where(oportunidade => oportunidade.EstaParada(funil, agora))]);
    }
}
