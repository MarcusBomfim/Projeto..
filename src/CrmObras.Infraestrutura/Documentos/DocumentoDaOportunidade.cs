using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma oportunidade é gravada: o título, o vendedor, cada passagem pelas
/// etapas, o que ela já tem para avançar e como terminou. A conta e o funil
/// são outros agregados, referenciados pelas colunas <c>conta_id</c> e
/// <c>funil_id</c>, e não copiados para cá.
/// </summary>
internal sealed record DocumentoDaOportunidade
{
    public const short Formato = 1;

    public required string Titulo { get; init; }

    public required string Responsavel { get; init; }

    public required DateTimeOffset CriadaEm { get; init; }

    /// <summary>O lead de onde a venda veio.</summary>
    public required Guid? Lead { get; init; }

    public required SituacaoDaOportunidade Situacao { get; init; }

    public required IReadOnlyList<PassagemGravada> Passagens { get; init; }

    public required decimal? ValorEstimado { get; init; }

    public required DateOnly? PrevisaoDeFechamento { get; init; }

    public required Guid? QuemDecide { get; init; }

    public required decimal? ValorFechado { get; init; }

    public required PerdaGravada? Perda { get; init; }

    public required DateTimeOffset? FechadaEm { get; init; }

    public static DocumentoDaOportunidade De(Oportunidade oportunidade)
    {
        var estado = oportunidade.Estado;

        return new()
        {
            Titulo = estado.Titulo,
            Responsavel = estado.Responsavel,
            CriadaEm = estado.CriadaEm,
            Lead = estado.Lead?.Valor,
            Situacao = estado.Situacao,
            Passagens = [.. estado.Passagens.Select(PassagemGravada.De)],
            ValorEstimado = estado.ValorEstimado?.Valor,
            PrevisaoDeFechamento = estado.PrevisaoDeFechamento,
            QuemDecide = estado.QuemDecide?.Valor,
            ValorFechado = estado.ValorFechado?.Valor,
            Perda = estado.Perda is { } perda ? PerdaGravada.De(perda) : null,
            FechadaEm = estado.FechadaEm,
        };
    }

    /// <remarks>As passagens, os valores e a situação são conferidos pelas regras que valem sempre (ADR-0010).</remarks>
    public Oportunidade ParaOportunidade(OportunidadeId id, ContaId conta, FunilId funil) =>
        Oportunidade.Reconstituir(
            id,
            new EstadoDaOportunidade(
                conta,
                Titulo,
                funil,
                Responsavel,
                CriadaEm,
                Lead is { } lead ? new LeadId(lead) : null,
                [.. Passagens.Select(passagem => passagem.ParaPassagem())],
                Situacao,
                ValorEstimado is { } estimado ? Dinheiro.Reais(estimado) : null,
                PrevisaoDeFechamento,
                QuemDecide is { } quemDecide ? new ContatoId(quemDecide) : null,
                ValorFechado is { } fechado ? Dinheiro.Reais(fechado) : null,
                Perda?.ParaPerda(),
                FechadaEm));
}

internal sealed record PassagemGravada
{
    public required Guid Etapa { get; init; }

    public required DateTimeOffset EntrouEm { get; init; }

    public required DateTimeOffset? SaiuEm { get; init; }

    public static PassagemGravada De(PassagemPorEtapa passagem) => new()
    {
        Etapa = passagem.Etapa.Valor,
        EntrouEm = passagem.EntrouEm,
        SaiuEm = passagem.SaiuEm,
    };

    public PassagemPorEtapa ParaPassagem() => new(new EtapaId(Etapa), EntrouEm, SaiuEm);
}

internal sealed record PerdaGravada
{
    public required MotivoDaPerda Motivo { get; init; }

    public required string? Concorrente { get; init; }

    public required string? Observacao { get; init; }

    public static PerdaGravada De(Perda perda) => new()
    {
        Motivo = perda.Motivo,
        Concorrente = perda.Concorrente,
        Observacao = perda.Observacao,
    };

    public Perda ParaPerda() => new(Motivo, Concorrente, Observacao);
}
