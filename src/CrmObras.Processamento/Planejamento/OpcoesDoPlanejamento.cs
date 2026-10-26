using Microsoft.Extensions.Options;

namespace CrmObras.Processamento.Planejamento;

/// <summary>
/// Onde está o planejamento-obras e como o processador se apresenta a ele: o
/// endereço da API e o emissor, a audiência e a chave do token de serviço
/// (ADR-0017). Vêm da seção <c>Planejamento</c> da configuração; a chave, que
/// é segredo, do ambiente.
/// </summary>
public sealed class OpcoesDoPlanejamento
{
    public const string Secao = "Planejamento";

    /// <summary>O HS256 pede uma chave de 256 bits, pelo menos.</summary>
    private const int MenorChaveEmBytes = 32;

    /// <summary>O endereço da API do planejamento: <c>http://planejamento-api:8080/</c>.</summary>
    public Uri? Endereco { get; set; }

    /// <summary>Quem emite o token de serviço: o emissor que o planejamento aceita para o CRM.</summary>
    public string Emissor { get; set; } = string.Empty;

    /// <summary>A audiência do token: a que a API do planejamento confere.</summary>
    public string Audiencia { get; set; } = string.Empty;

    /// <summary>A chave simétrica com que o token é assinado, em Base64.</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Quanto esperar a resposta do planejamento. Esgotado, é uma falha do tratamento, e a mensagem volta mais tarde.</summary>
    public TimeSpan TempoLimite { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>A chave em bytes; só depois de validada.</summary>
    internal byte[] ChaveEmBytes => Convert.FromBase64String(Chave);

    /// <summary>O que não serve na configuração, numa frase cada.</summary>
    public static IReadOnlyList<string> Problemas(OpcoesDoPlanejamento opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        var problemas = new List<string>();

        if (opcoes.Endereco is not { IsAbsoluteUri: true } endereco || (endereco.Scheme != Uri.UriSchemeHttp && endereco.Scheme != Uri.UriSchemeHttps))
        {
            problemas.Add($"{Secao}:Endereco é o endereço absoluto da API do planejamento-obras, como http://planejamento-api:8080/.");
        }

        if (string.IsNullOrWhiteSpace(opcoes.Emissor))
        {
            problemas.Add($"{Secao}:Emissor é o emissor do token de serviço, que o planejamento-obras aceita para o CRM.");
        }

        if (string.IsNullOrWhiteSpace(opcoes.Audiencia))
        {
            problemas.Add($"{Secao}:Audiencia é a audiência que a API do planejamento-obras confere no token.");
        }

        if (!ChaveServe(opcoes.Chave))
        {
            problemas.Add($"{Secao}:Chave é a chave do token de serviço, em Base64, com {MenorChaveEmBytes} bytes ou mais.");
        }

        if (opcoes.TempoLimite <= TimeSpan.Zero)
        {
            problemas.Add($"{Secao}:TempoLimite é um tempo maior que zero.");
        }

        return problemas;
    }

    private static bool ChaveServe(string chave)
    {
        var bytes = new byte[Math.Max(chave.Length, 1)];

        return Convert.TryFromBase64String(chave, bytes, out var tamanho) && tamanho >= MenorChaveEmBytes;
    }
}

/// <summary>Confere a configuração do planejamento quando o host sobe: sem ela, a venda ganha não chegaria a lugar nenhum.</summary>
internal sealed class ValidacaoDoPlanejamento : IValidateOptions<OpcoesDoPlanejamento>
{
    public ValidateOptionsResult Validate(string? name, OpcoesDoPlanejamento options) =>
        OpcoesDoPlanejamento.Problemas(options) is { Count: > 0 } problemas
            ? ValidateOptionsResult.Fail(problemas)
            : ValidateOptionsResult.Success;
}
