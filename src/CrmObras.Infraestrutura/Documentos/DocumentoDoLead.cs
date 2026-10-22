using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Leads;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como um lead é gravado: o que ele deixou ao chegar, e o que aconteceu com
/// ele desde então — a avaliação, a qualificação, o descarte, a conversão.
/// </summary>
internal sealed record DocumentoDoLead
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    public required string? Empresa { get; init; }

    /// <summary>O CNPJ ou o CPF, sem máscara.</summary>
    public required string? Documento { get; init; }

    public required string? Email { get; init; }

    /// <summary>No formato internacional: <c>+5511987654321</c>.</summary>
    public required string? Telefone { get; init; }

    public required string? Interesse { get; init; }

    public required OrigemDoLead Origem { get; init; }

    public required string? DetalheDaOrigem { get; init; }

    public required DateTimeOffset ChegouEm { get; init; }

    public required SituacaoDoLead Situacao { get; init; }

    public required AvaliacaoGravada Avaliacao { get; init; }

    public required DateTimeOffset? QualificadoEm { get; init; }

    public required MotivoDoDescarte? MotivoDoDescarte { get; init; }

    public required string? ObservacaoDoDescarte { get; init; }

    public required DateTimeOffset? DescartadoEm { get; init; }

    /// <summary>A conta em que o lead virou cliente.</summary>
    public required Guid? Conta { get; init; }

    /// <summary>O contato que o lead virou, dentro da conta.</summary>
    public required Guid? Contato { get; init; }

    public required DateTimeOffset? ConvertidoEm { get; init; }

    public static DocumentoDoLead De(Lead lead)
    {
        var estado = lead.Estado;

        return new()
        {
            Nome = lead.Nome,
            Empresa = lead.Empresa,
            Documento = lead.Documento?.Numero,
            Email = lead.Email?.Endereco,
            Telefone = lead.Telefone?.Internacional,
            Interesse = lead.Interesse,
            Origem = lead.Origem,
            DetalheDaOrigem = lead.DetalheDaOrigem,
            ChegouEm = lead.ChegouEm,
            Situacao = estado.Situacao,
            Avaliacao = AvaliacaoGravada.De(estado.Avaliacao),
            QualificadoEm = estado.QualificadoEm,
            MotivoDoDescarte = estado.MotivoDoDescarte,
            ObservacaoDoDescarte = estado.ObservacaoDoDescarte,
            DescartadoEm = estado.DescartadoEm,
            Conta = estado.Conta?.Valor,
            Contato = estado.Contato?.Valor,
            ConvertidoEm = estado.ConvertidoEm,
        };
    }

    /// <remarks>Os passos são dados de novo pelas regras de sempre, e têm de chegar ao estado gravado (ADR-0010).</remarks>
    public Lead ParaLead(LeadId id) =>
        Lead.Reconstituir(
            id,
            new DadosDoLead(
                Nome,
                Empresa,
                Documento is null ? null : Dominio.Identificacao.Documento.De(Documento),
                Email is null ? null : Dominio.MeiosDeContato.Email.De(Email),
                Telefone is null ? null : Dominio.MeiosDeContato.Telefone.De(Telefone),
                Interesse),
            Origem,
            DetalheDaOrigem,
            ChegouEm,
            new EstadoDoLead(
                Situacao,
                Avaliacao.ParaAvaliacao(),
                QualificadoEm,
                MotivoDoDescarte,
                ObservacaoDoDescarte,
                DescartadoEm,
                Conta is { } conta ? new ContaId(conta) : null,
                Contato is { } contato ? new ContatoId(contato) : null,
                ConvertidoEm));
}

internal sealed record AvaliacaoGravada
{
    public required Resposta Orcamento { get; init; }

    public required Resposta FalaComQuemDecide { get; init; }

    public required Resposta Necessidade { get; init; }

    public required Resposta Prazo { get; init; }

    public static AvaliacaoGravada De(Avaliacao avaliacao) => new()
    {
        Orcamento = avaliacao.Orcamento,
        FalaComQuemDecide = avaliacao.FalaComQuemDecide,
        Necessidade = avaliacao.Necessidade,
        Prazo = avaliacao.Prazo,
    };

    public Avaliacao ParaAvaliacao() => new(Orcamento, FalaComQuemDecide, Necessidade, Prazo);
}
