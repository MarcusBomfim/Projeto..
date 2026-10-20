using CrmObras.Dominio.Comum;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Clientes;

/// <summary>O que se informa de um contato — para adicionar, para alterar, para converter um lead.</summary>
/// <param name="Nome">Nome de quem atende.</param>
/// <param name="Email">E-mail; o contato precisa de e-mail ou de telefone.</param>
/// <param name="Telefone">Telefone; o contato precisa de e-mail ou de telefone.</param>
/// <param name="Cargo">Cargo na empresa do cliente, como ele diz: "Diretor de obras".</param>
/// <param name="Papel">O papel na compra.</param>
public sealed record DadosDoContato(
    string Nome,
    Email? Email = null,
    Telefone? Telefone = null,
    string? Cargo = null,
    PapelNaCompra Papel = PapelNaCompra.NaoSabido);

/// <summary>Uma pessoa com quem se fala, numa conta.</summary>
/// <remarks>
/// Só muda pela conta dela: é a conta que sabe se o e-mail já é de outro
/// contato, ou se a pessoa é a principal.
/// </remarks>
public sealed class Contato
{
    internal Contato(ContatoId id, DadosDoContato dados)
    {
        Id = id;
        Nome = string.Empty;
        Alterar(dados);
    }

    public ContatoId Id { get; }

    public string Nome { get; private set; }

    public string? Cargo { get; private set; }

    public Email? Email { get; private set; }

    public Telefone? Telefone { get; private set; }

    public PapelNaCompra Papel { get; private set; }

    internal void Alterar(DadosDoContato dados)
    {
        Nome = dados.Nome.Trim();
        Cargo = Textos.Opcional(dados.Cargo);
        Email = dados.Email;
        Telefone = dados.Telefone;
        Papel = dados.Papel;
    }

    public override string ToString() => Nome;
}
