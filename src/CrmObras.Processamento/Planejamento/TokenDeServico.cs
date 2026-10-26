using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrmObras.Processamento.Planejamento;

/// <summary>
/// O token com que o processador se apresenta ao planejamento-obras: um JWT
/// assinado com a chave que o planejamento aceita para o CRM, em nome da
/// construtora da mensagem e com o papel de quem planeja (ADR-0017).
/// </summary>
/// <remarks>
/// <para>
/// Um token por pedido, válido por cinco minutos: o que vazar de um log não
/// serve para muito, e não há token guardado para renovar.
/// </para>
/// <para>
/// A construtora vai na reivindicação <c>empresa</c>, que é como o
/// planejamento a chama: o id é o mesmo nos dois sistemas, o do provedor de
/// identidade da construtora. O papel é só o de planejador — o processador
/// abre cronogramas, e não aprova linha de base nem mede obra.
/// </para>
/// </remarks>
public sealed class TokenDeServico(IOptions<OpcoesDoPlanejamento> opcoes, TimeProvider relogio)
{
    /// <summary>Quem o token diz que é, no planejamento.</summary>
    public const string Sujeito = "crm-obras";

    public const string Nome = "CRM de Obras";

    public const string Papel = "planejador";

    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    private static readonly JsonWebTokenHandler Emissor = new() { SetDefaultTimesOnTokenCreation = false };

    public string Para(Guid construtora)
    {
        if (construtora == Guid.Empty)
        {
            throw new ArgumentException("O token de serviço é em nome de uma construtora; o Guid vazio não é uma.", nameof(construtora));
        }

        var configuracao = opcoes.Value;
        var agora = relogio.GetUtcNow().UtcDateTime;

        return Emissor.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuracao.Emissor,
            Audience = configuracao.Audiencia,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Sujeito,
                ["name"] = Nome,
                ["empresa"] = construtora.ToString(),
                ["role"] = Papel,
            },
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora + Validade,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(configuracao.ChaveEmBytes), SecurityAlgorithms.HmacSha256),
        });
    }
}
