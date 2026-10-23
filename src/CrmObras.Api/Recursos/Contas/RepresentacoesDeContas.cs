using CrmObras.Dominio.Clientes;

namespace CrmObras.Api.Recursos.Contas;

/// <summary>
/// Uma conta nova: o nome, o CNPJ ou o CPF, se já se sabe, e, se quiser, os
/// contatos de uma vez. O primeiro contato é o principal.
/// </summary>
public sealed record NovaConta(string Nome, string? Documento = null, IReadOnlyList<NovoContato>? Contatos = null);

/// <summary>
/// O que muda na conta; o que não vier fica como está. O documento só se
/// informa na conta que não tem um: informado, ele identifica a conta e não
/// muda (ADR-0002).
/// </summary>
public sealed record AlteracaoDaConta(string? Nome = null, string? Documento = null);

/// <summary>Uma pessoa da conta: o nome, um e-mail ou um telefone — ou os dois —, o cargo e o papel na compra.</summary>
public sealed record NovoContato(
    string Nome,
    string? Email = null,
    string? Telefone = null,
    string? Cargo = null,
    PapelNaCompra Papel = PapelNaCompra.NaoSabido)
{
    public DadosDoContato ParaDados() => new(Nome, Email.ComoEmail(), Telefone.ComoTelefone(), Cargo, Papel);
}

/// <summary>Quem passa a ser o contato principal da conta.</summary>
public sealed record NovoPrincipal(Guid Contato);

public sealed record ContaResposta(Guid Id, string Nome, string? Documento, IReadOnlyList<ContatoResposta> Contatos, Guid? Principal)
{
    public static ContaResposta De(Conta conta)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return new(
            conta.Id.Valor,
            conta.Nome,
            conta.Documento?.Formatado,
            [.. conta.Contatos.Select(contato => new ContatoResposta(
                contato.Id.Valor,
                contato.Nome,
                contato.Cargo,
                contato.Email?.Endereco,
                contato.Telefone?.Formatado,
                contato.Papel))],
            conta.Principal?.Id.Valor);
    }
}

public sealed record ContatoResposta(Guid Id, string Nome, string? Cargo, string? Email, string? Telefone, PapelNaCompra Papel);
