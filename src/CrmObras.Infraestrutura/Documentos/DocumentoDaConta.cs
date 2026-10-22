using CrmObras.Dominio.Clientes;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma conta é gravada: o nome, o documento, os contatos e o principal
/// entre eles. O CNPJ ou o CPF vai sem máscara; o telefone, no formato
/// internacional — os dois como o domínio os lê de volta.
/// </summary>
internal sealed record DocumentoDaConta
{
    public const short Formato = 1;

    public required string Nome { get; init; }

    /// <summary>O CNPJ ou o CPF, sem máscara.</summary>
    public required string? Documento { get; init; }

    public required Guid? Principal { get; init; }

    public required IReadOnlyList<ContatoGravado> Contatos { get; init; }

    public static DocumentoDaConta De(Conta conta) => new()
    {
        Nome = conta.Nome,
        Documento = conta.Documento?.Numero,
        Principal = conta.Principal?.Id.Valor,
        Contatos = [.. conta.Contatos.Select(ContatoGravado.De)],
    };

    /// <remarks>Os contatos entram pela conferência de sempre: e-mail repetido é recusado na leitura (ADR-0010).</remarks>
    public Conta ParaConta(ContaId id) =>
        Conta.Reconstituir(
            id,
            Nome,
            Documento is null ? null : Dominio.Identificacao.Documento.De(Documento),
            [.. Contatos.Select(contato => (new ContatoId(contato.Id), contato.ParaDados()))],
            Principal is { } principal ? new ContatoId(principal) : null);
}

internal sealed record ContatoGravado
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public required string? Email { get; init; }

    /// <summary>No formato internacional: <c>+5511987654321</c>.</summary>
    public required string? Telefone { get; init; }

    public required string? Cargo { get; init; }

    public required PapelNaCompra Papel { get; init; }

    public static ContatoGravado De(Contato contato) => new()
    {
        Id = contato.Id.Valor,
        Nome = contato.Nome,
        Email = contato.Email?.Endereco,
        Telefone = contato.Telefone?.Internacional,
        Cargo = contato.Cargo,
        Papel = contato.Papel,
    };

    public DadosDoContato ParaDados() =>
        new(
            Nome,
            Email is null ? null : Dominio.MeiosDeContato.Email.De(Email),
            Telefone is null ? null : Dominio.MeiosDeContato.Telefone.De(Telefone),
            Cargo,
            Papel);
}
