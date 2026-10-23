using System.Security.Claims;
using System.Security.Cryptography;
using CrmObras.Api.Seguranca;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrmObras.Api.Testes;

/// <summary>
/// Os tokens dos testes, emitidos aqui mesmo com uma chave gerada a cada
/// execução — no lugar do provedor de identidade.
/// </summary>
internal static class Tokens
{
    public const string Emissor = "https://identidade.testes";

    public const string Audiencia = "crm-obras";

    public static readonly SymmetricSecurityKey Chave = new(RandomNumberGenerator.GetBytes(32));

    /// <summary>O token de alguém da construtora, com o login e os papéis dados — válido por uma hora.</summary>
    public static string De(Guid construtora, string login, params string[] papeis) =>
        Emitir(construtora.ToString(), login, papeis, Chave, Emissor, Audiencia, DateTime.UtcNow.AddHours(1));

    public static string Emitir(
        string? construtora,
        string? login,
        IEnumerable<string> papeis,
        SecurityKey chave,
        string emissor,
        string audiencia,
        DateTime venceEm)
    {
        var reivindicacoes = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new(Reivindicacoes.Nome, "Pessoa dos testes"),
        };

        if (construtora is not null)
        {
            reivindicacoes.Add(new Claim(Reivindicacoes.Construtora, construtora));
        }

        if (login is not null)
        {
            reivindicacoes.Add(new Claim(Reivindicacoes.Login, login));
        }

        reivindicacoes.AddRange(papeis.Select(papel => new Claim(Reivindicacoes.Papel, papel)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = audiencia,
            Subject = new ClaimsIdentity(reivindicacoes),
            IssuedAt = venceEm.AddHours(-2),
            NotBefore = venceEm.AddHours(-2),
            Expires = venceEm,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256),
        });
    }
}
