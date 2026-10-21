using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Atividades;

/// <summary>
/// O dia de um vendedor: o que está atrasado, o que falta fazer hoje e as
/// vendas dele sem próximo passo (ADR-0006).
/// </summary>
/// <remarks>
/// <para>
/// O dia é o de quem olha a agenda: a atividade marcada para 23h em Brasília
/// é de hoje para quem está em Brasília, e de amanhã para quem está em Lisboa.
/// O fuso é o do instante <c>agora</c>.
/// </para>
/// <para>
/// Venda aberta sem atividade pendente é venda sem próximo passo — a que
/// esfria sem que ninguém perceba. A atividade atrasada ainda é o próximo
/// passo: está marcada, só não foi feita; e ela aparece nas atrasadas.
/// </para>
/// </remarks>
public sealed class Agenda
{
    private Agenda(DateOnly dia, IReadOnlyList<Atividade> atrasadas, IReadOnlyList<Atividade> deHoje, IReadOnlyList<Oportunidade> semProximoPasso)
    {
        Dia = dia;
        Atrasadas = atrasadas;
        DeHoje = deHoje;
        SemProximoPasso = semProximoPasso;
    }

    public DateOnly Dia { get; }

    /// <summary>As pendentes cuja hora passou, da mais antiga para a mais recente.</summary>
    public IReadOnlyList<Atividade> Atrasadas { get; }

    /// <summary>As pendentes do resto do dia, pela hora.</summary>
    public IReadOnlyList<Atividade> DeHoje { get; }

    /// <summary>As vendas abertas do vendedor sem atividade pendente, da que está parada na etapa há mais tempo.</summary>
    public IReadOnlyList<Oportunidade> SemProximoPasso { get; }

    /// <param name="responsavel">O login do vendedor.</param>
    /// <param name="atividades">As atividades do vendedor, e as das vendas dele — o próximo passo pode ser de outro, como a visita do engenheiro.</param>
    /// <param name="oportunidades">As vendas do vendedor.</param>
    /// <param name="agora">O instante, no fuso de quem olha.</param>
    public static Agenda De(string responsavel, IEnumerable<Atividade> atividades, IEnumerable<Oportunidade> oportunidades, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(atividades);
        ArgumentNullException.ThrowIfNull(oportunidades);

        var pendentes = atividades.Where(atividade => atividade.Situacao == SituacaoDaAtividade.Pendente).ToList();
        var minhas = pendentes.Where(atividade => atividade.Responsavel == responsavel).ToList();
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        var comProximoPasso = pendentes.Select(atividade => atividade.Oportunidade).OfType<OportunidadeId>().ToHashSet();

        return new Agenda(
            hoje,
            [.. minhas.Where(atividade => atividade.EstaAtrasada(agora)).OrderBy(atividade => atividade.PrevistaPara)],
            [
                .. minhas
                    .Where(atividade => !atividade.EstaAtrasada(agora) && DateOnly.FromDateTime(atividade.PrevistaPara.ToOffset(agora.Offset).DateTime) == hoje)
                    .OrderBy(atividade => atividade.PrevistaPara),
            ],
            [
                .. oportunidades
                    .Where(oportunidade => oportunidade.Responsavel == responsavel
                        && oportunidade.Situacao == SituacaoDaOportunidade.Aberta
                        && !comProximoPasso.Contains(oportunidade.Id))
                    .OrderBy(oportunidade => oportunidade.EntrouNaEtapaEm),
            ]);
    }
}
