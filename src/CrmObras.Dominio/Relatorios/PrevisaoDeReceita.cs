using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Relatorios;

/// <summary>Um mês da previsão: o que já foi ganho nele e o que deve fechar.</summary>
/// <param name="Mes">O mês.</param>
/// <param name="Ganho">A soma dos valores fechados das vendas ganhas no mês.</param>
/// <param name="EmAberto">A soma dos valores estimados das vendas abertas que devem fechar no mês.</param>
/// <param name="Ponderado">O em aberto multiplicado pela probabilidade da etapa de cada venda, ao centavo.</param>
/// <param name="Abertas">As vendas abertas que devem fechar no mês, pela data da previsão.</param>
public sealed record MesDaPrevisao(Mes Mes, Dinheiro Ganho, Dinheiro EmAberto, Dinheiro Ponderado, IReadOnlyList<Oportunidade> Abertas)
{
    /// <summary>O ganho mais o ponderado: o que o mês deve render.</summary>
    public Dinheiro Previsto => Ganho + Ponderado;
}

/// <summary>
/// Quanto as vendas devem render, mês a mês: o que já foi ganho e o que está
/// em aberto, ponderado pela etapa de cada venda (ADR-0008).
/// </summary>
/// <remarks>
/// <para>
/// A venda aberta entra no mês da previsão de fechamento, pelo valor estimado
/// vezes a probabilidade da etapa em que está. A ganha entra no mês em que
/// foi ganha, pelo valor fechado. A perdida não entra.
/// </para>
/// <para>
/// A previsão não chuta. A venda sem valor, a sem data e a de previsão
/// vencida — devia ter fechado, e não fechou — ficam fora dos meses, cada uma
/// na sua lista, para quem cuida dela corrigir.
/// </para>
/// </remarks>
public sealed class PrevisaoDeReceita
{
    private const int MaisMeses = 24;

    private PrevisaoDeReceita(
        IReadOnlyList<MesDaPrevisao> meses,
        IReadOnlyList<Oportunidade> vencidas,
        IReadOnlyList<Oportunidade> semValor,
        IReadOnlyList<Oportunidade> semPrevisao)
    {
        Meses = meses;
        Vencidas = vencidas;
        SemValor = semValor;
        SemPrevisao = semPrevisao;
    }

    public IReadOnlyList<MesDaPrevisao> Meses { get; }

    /// <summary>As abertas com previsão de fechamento antes de hoje, da mais atrasada.</summary>
    public IReadOnlyList<Oportunidade> Vencidas { get; }

    /// <summary>As abertas sem valor estimado.</summary>
    public IReadOnlyList<Oportunidade> SemValor { get; }

    /// <summary>As abertas com valor, mas sem previsão de fechamento.</summary>
    public IReadOnlyList<Oportunidade> SemPrevisao { get; }

    public Dinheiro Ganho => Meses.Aggregate(Dinheiro.Zero, (soma, mes) => soma + mes.Ganho);

    public Dinheiro Ponderado => Meses.Aggregate(Dinheiro.Zero, (soma, mes) => soma + mes.Ponderado);

    public Dinheiro Previsto => Ganho + Ponderado;

    /// <param name="funis">Os funis das vendas, para a probabilidade de cada etapa.</param>
    /// <param name="oportunidades">As vendas — de um vendedor, de uma equipe, da construtora.</param>
    /// <param name="primeiro">O primeiro mês da previsão.</param>
    /// <param name="meses">Quantos meses, de 1 a 24.</param>
    /// <param name="agora">O instante: dele vêm o dia de hoje e o fuso em que se conta o mês do ganho.</param>
    public static PrevisaoDeReceita Do(IEnumerable<Funil> funis, IEnumerable<Oportunidade> oportunidades, Mes primeiro, int meses, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(funis);
        ArgumentNullException.ThrowIfNull(oportunidades);
        ArgumentNullException.ThrowIfNull(primeiro);

        if (meses is < 1 or > MaisMeses)
        {
            throw new ExcecaoDeDominio($"A previsão vai de 1 a {MaisMeses} meses: {meses} não serve.");
        }

        var porId = funis.ToDictionary(funil => funil.Id);
        var lista = oportunidades.ToList();
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        var abertas = lista.Where(oportunidade => oportunidade.Situacao == SituacaoDaOportunidade.Aberta).ToList();
        var comValor = abertas.Where(oportunidade => oportunidade.ValorEstimado is not null).ToList();
        var previstas = comValor.Where(oportunidade => oportunidade.PrevisaoDeFechamento >= hoje).ToList();
        var ganhas = lista.Where(oportunidade => oportunidade.Situacao == SituacaoDaOportunidade.Ganha).ToList();

        return new PrevisaoDeReceita(
            [
                .. Enumerable.Range(0, meses)
                    .Select(primeiro.Mais)
                    .Select(mes => DoMes(
                        mes,
                        previstas.Where(oportunidade => Mes.De(oportunidade.PrevisaoDeFechamento!.Value) == mes),
                        ganhas.Where(oportunidade => Mes.De(DateOnly.FromDateTime(oportunidade.FechadaEm!.Value.ToOffset(agora.Offset).DateTime)) == mes),
                        porId)),
            ],
            [.. comValor.Where(oportunidade => oportunidade.PrevisaoDeFechamento < hoje).OrderBy(oportunidade => oportunidade.PrevisaoDeFechamento)],
            [.. abertas.Where(oportunidade => oportunidade.ValorEstimado is null)],
            [.. comValor.Where(oportunidade => oportunidade.PrevisaoDeFechamento is null)]);
    }

    private static MesDaPrevisao DoMes(Mes mes, IEnumerable<Oportunidade> previstas, IEnumerable<Oportunidade> ganhas, Dictionary<FunilId, Funil> funis)
    {
        List<Oportunidade> abertas = [.. previstas.OrderBy(oportunidade => oportunidade.PrevisaoDeFechamento)];

        return new MesDaPrevisao(
            mes,
            ganhas.Aggregate(Dinheiro.Zero, (soma, ganha) => soma + ganha.ValorFechado!.Value),
            abertas.Aggregate(Dinheiro.Zero, (soma, aberta) => soma + aberta.ValorEstimado!.Value),
            abertas
                .Aggregate(Dinheiro.Zero, (soma, aberta) => soma + (aberta.ValorEstimado!.Value * (Probabilidade(aberta, funis) / 100m)))
                .EmCentavos(),
            abertas);
    }

    private static int Probabilidade(Oportunidade oportunidade, Dictionary<FunilId, Funil> funis) =>
        funis.TryGetValue(oportunidade.Funil, out var funil)
            ? funil.Etapa(oportunidade.Etapa).Probabilidade
            : throw new ArgumentException($"Falta o funil da oportunidade {oportunidade.Titulo}.", nameof(funis));
}
