using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma atividade é gravada: o que, quando, com quem e por quem — e como
/// terminou. A conta e a venda são outros agregados, referenciados pelas
/// colunas <c>conta_id</c> e <c>oportunidade_id</c>.
/// </summary>
internal sealed record DocumentoDaAtividade
{
    public const short Formato = 1;

    public required TipoDeAtividade Tipo { get; init; }

    public required string Assunto { get; init; }

    public required DateTimeOffset PrevistaPara { get; init; }

    /// <summary>O contato do cliente com quem se vai falar.</summary>
    public required Guid? Com { get; init; }

    public required string? Detalhes { get; init; }

    public required string Responsavel { get; init; }

    public required DateTimeOffset CriadaEm { get; init; }

    public required int Adiamentos { get; init; }

    public required SituacaoDaAtividade Situacao { get; init; }

    public required string? Resultado { get; init; }

    public required string? MotivoDoCancelamento { get; init; }

    public required DateTimeOffset? EncerradaEm { get; init; }

    public static DocumentoDaAtividade De(Atividade atividade)
    {
        var estado = atividade.Estado;

        return new()
        {
            Tipo = estado.Dados.Tipo,
            Assunto = estado.Dados.Assunto,
            PrevistaPara = estado.Dados.PrevistaPara,
            Com = estado.Dados.Com?.Valor,
            Detalhes = estado.Dados.Detalhes,
            Responsavel = estado.Responsavel,
            CriadaEm = estado.CriadaEm,
            Adiamentos = estado.Adiamentos,
            Situacao = estado.Situacao,
            Resultado = estado.Resultado,
            MotivoDoCancelamento = estado.MotivoDoCancelamento,
            EncerradaEm = estado.EncerradaEm,
        };
    }

    /// <remarks>Os textos e a situação são conferidos pelas regras que valem sempre (ADR-0010).</remarks>
    public Atividade ParaAtividade(AtividadeId id, ContaId conta, OportunidadeId? oportunidade) =>
        Atividade.Reconstituir(
            id,
            new EstadoDaAtividade(
                conta,
                oportunidade,
                new DadosDaAtividade(Tipo, Assunto, PrevistaPara, Com is { } com ? new ContatoId(com) : null, Detalhes),
                Responsavel,
                CriadaEm,
                Adiamentos,
                Situacao,
                Resultado,
                MotivoDoCancelamento,
                EncerradaEm));
}
