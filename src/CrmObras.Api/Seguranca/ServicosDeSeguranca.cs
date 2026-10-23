using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace CrmObras.Api.Seguranca;

/// <summary>Autenticação por JWT e as políticas de autorização (ADR-0014).</summary>
internal static class ServicosDeSeguranca
{
    /// <remarks>
    /// <para>
    /// Quem emite o token não é a API: um provedor de identidade, configurado
    /// em <c>Authentication:Schemes:Bearer</c> — em desenvolvimento, o
    /// <c>dotnet user-jwts</c>. A API só confere assinatura, emissor,
    /// audiência e validade.
    /// </para>
    /// <para>
    /// Toda política começa por ser da construtora, e é também a política de
    /// reserva: endpoint que esquecer de declarar uma ainda exige ao menos
    /// isso. Anônimo, só o documento OpenAPI.
    /// </para>
    /// </remarks>
    public static IServiceCollection AdicionarSeguranca(this IServiceCollection servicos)
    {
        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                // As reivindicações com os nomes do token — role, construtora —,
                // sem a tradução para os URIs longos do WS-Federation.
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters.NameClaimType = Reivindicacoes.Nome;
                opcoes.TokenValidationParameters.RoleClaimType = Reivindicacoes.Papel;
            });

        var daConstrutora = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto => ConstrutoraDoUsuario.Ler(contexto.User) is not null)
            .Build();

        servicos.AddAuthorizationBuilder()
            .SetDefaultPolicy(daConstrutora)
            .SetFallbackPolicy(daConstrutora)
            .AddPolicy(Politicas.Consultar, daConstrutora)
            .AddPolicy(Politicas.Vender, politica => politica.Combine(daConstrutora).RequireRole(Papeis.Vendedor, Papeis.Gerente, Papeis.Diretor))
            .AddPolicy(Politicas.Gerir, politica => politica.Combine(daConstrutora).RequireRole(Papeis.Gerente, Papeis.Diretor));

        return servicos;
    }
}
