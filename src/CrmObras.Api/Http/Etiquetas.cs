using System.Globalization;

namespace CrmObras.Api.Http;

/// <summary>
/// A versão do agregado como ETag forte — <c>"3"</c> — e de volta
/// (ADR-0012, ADR-0014).
/// </summary>
internal static class Etiquetas
{
    public static string Da(int versao) => string.Create(CultureInfo.InvariantCulture, $"\"{versao}\"");

    public static void Escrever(HttpContext contexto, int versao) => contexto.Response.Headers.ETag = Da(versao);

    /// <summary>Lê uma ETag forte com uma versão: <c>"3"</c>. Fraca, lista, <c>*</c> ou qualquer outra coisa não serve.</summary>
    public static bool TentarLer(string? etiqueta, out int versao)
    {
        versao = 0;

        return etiqueta is ['"', .. var numero, '"']
            && numero.Length is > 0 and <= 9
            && numero.All(char.IsAsciiDigit)
            && int.TryParse(numero, NumberStyles.None, CultureInfo.InvariantCulture, out versao)
            && versao >= 1;
    }

    /// <summary>Se o cliente já tem esta versão (<c>If-None-Match</c>) — e a resposta pode ser 304.</summary>
    public static bool ClienteJaTem(HttpContext contexto, int versao)
    {
        var atual = Da(versao);

        return contexto.Request.Headers.IfNoneMatch
            .SelectMany(valor => (valor ?? string.Empty).Split(','))
            .Select(etiqueta => etiqueta.Trim())
            .Any(etiqueta => etiqueta == "*" || etiqueta == atual || etiqueta == $"W/{atual}");
    }
}
