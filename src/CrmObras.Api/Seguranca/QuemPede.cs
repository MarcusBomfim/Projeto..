using System.Security.Claims;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Seguranca;

/// <summary>
/// A construtora de quem fez o pedido, lida do token: é em nome dela que a
/// persistência lê e grava (ADR-0011).
/// </summary>
/// <remarks>
/// As políticas já recusam token sem construtora válida, então dentro de um
/// endpoint ela sempre existe. Fora de pedido — ou com um token sem ela —, é
/// nenhuma construtora: nada se lê, nada se grava.
/// </remarks>
public sealed class ConstrutoraDoUsuario(IHttpContextAccessor acessor) : IConstrutoraAtual
{
    public Guid? Id => Ler(acessor.HttpContext?.User);

    /// <summary>A construtora do token, se houver uma válida.</summary>
    public static Guid? Ler(ClaimsPrincipal? usuario) =>
        Guid.TryParse(usuario?.FindFirstValue(Reivindicacoes.Construtora), out var construtora) && construtora != Guid.Empty
            ? construtora
            : null;
}

/// <summary>Quem do comercial fez o pedido, para o domínio: o login, e o cargo mais alto entre os papéis do token.</summary>
internal static class QuemPede
{
    public static Comercial Comercial(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var cargo = usuario.IsInRole(Papeis.Diretor) ? Cargo.Diretor
            : usuario.IsInRole(Papeis.Gerente) ? Cargo.Gerente
            : Cargo.Vendedor;

        return new Comercial(Login(usuario), cargo);
    }

    /// <summary>O login do token — o responsável pelo que se cria. Token sem login não cria nada em nome de ninguém.</summary>
    public static string Login(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return usuario.FindFirstValue(Reivindicacoes.Login) is { Length: > 0 } login
            ? login
            : throw new Http.PedidoInvalido($"O token não traz o login de quem pede, na reivindicação {Reivindicacoes.Login}.");
    }
}
