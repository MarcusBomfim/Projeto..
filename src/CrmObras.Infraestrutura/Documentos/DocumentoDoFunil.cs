using CrmObras.Dominio.Funis;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como um funil é gravado: as etapas em uso, na ordem da venda; as
/// arquivadas, na ordem em que saíram; e as duas finais, com o nome que
/// tiverem.
/// </summary>
internal sealed record DocumentoDoFunil
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required IReadOnlyList<EtapaGravada> Abertas { get; init; }

    public required IReadOnlyList<EtapaGravada> Arquivadas { get; init; }

    public required EtapaFinalGravada Ganha { get; init; }

    public required EtapaFinalGravada Perdida { get; init; }

    public static DocumentoDoFunil De(Funil funil) => new()
    {
        Nome = funil.Nome,
        Abertas = [.. funil.Abertas.Select(EtapaGravada.De)],
        Arquivadas = [.. funil.Arquivadas.Select(EtapaGravada.De)],
        Ganha = EtapaFinalGravada.De(funil.Ganha),
        Perdida = EtapaFinalGravada.De(funil.Perdida),
    };

    /// <remarks>As abertas passam pelas regras de quem cria o funil; as arquivadas, pelas de cada etapa (ADR-0010).</remarks>
    public Funil ParaFunil(FunilId id) =>
        Funil.Reconstituir(
            id,
            Nome,
            [.. Abertas.Select(etapa => etapa.ParaEtapa())],
            [.. Arquivadas.Select(etapa => etapa.ParaEtapa())],
            (new EtapaId(Ganha.Id), Ganha.Nome),
            (new EtapaId(Perdida.Id), Perdida.Nome));
}

internal sealed record EtapaGravada
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public required int Probabilidade { get; init; }

    public required int? DiasEsperados { get; init; }

    public required Requisitos Requisitos { get; init; }

    public static EtapaGravada De(Etapa etapa) => new()
    {
        Id = etapa.Id.Valor,
        Nome = etapa.Nome,
        Probabilidade = etapa.Probabilidade,
        DiasEsperados = etapa.DiasEsperados,
        Requisitos = etapa.Requisitos,
    };

    public (EtapaId Id, DadosDaEtapa Dados) ParaEtapa() => (new EtapaId(Id), new DadosDaEtapa(Nome, Probabilidade, DiasEsperados, Requisitos));
}

internal sealed record EtapaFinalGravada
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public static EtapaFinalGravada De(Etapa etapa) => new() { Id = etapa.Id.Valor, Nome = etapa.Nome };
}
