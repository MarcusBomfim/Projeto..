using System.Collections.Frozen;
using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.MeiosDeContato;

/// <summary>Um telefone brasileiro: DDD e número, de celular ou fixo.</summary>
/// <remarks>
/// <para>
/// Aceita o que as pessoas escrevem — <c>(11) 98765-4321</c>,
/// <c>+55 11 98765-4321</c>, <c>011 3333-4444</c> — e guarda só o DDD e o
/// número. Dois telefones escritos de jeitos diferentes são o mesmo telefone.
/// </para>
/// <para>
/// O DDD tem de existir no plano de numeração da Anatel. O celular tem 9
/// dígitos e começa com 9; o fixo tem 8 e começa de 2 a 5. Número de serviço —
/// 0800, 0300, 0500, 0900 — não é telefone de contato de cliente.
/// </para>
/// </remarks>
public sealed record Telefone
{
    /// <summary>Os DDDs do plano de numeração brasileiro.</summary>
    private static readonly FrozenSet<string> Ddds = new[]
    {
        "11", "12", "13", "14", "15", "16", "17", "18", "19",
        "21", "22", "24", "27", "28",
        "31", "32", "33", "34", "35", "37", "38",
        "41", "42", "43", "44", "45", "46", "47", "48", "49",
        "51", "53", "54", "55",
        "61", "62", "63", "64", "65", "66", "67", "68", "69",
        "71", "73", "74", "75", "77", "79",
        "81", "82", "83", "84", "85", "86", "87", "88", "89",
        "91", "92", "93", "94", "95", "96", "97", "98", "99",
    }.ToFrozenSet(StringComparer.Ordinal);

    private Telefone(string ddd, string numero)
    {
        Ddd = ddd;
        Numero = numero;
    }

    public string Ddd { get; }

    /// <summary>O número sem o DDD: 9 dígitos no celular, 8 no fixo.</summary>
    public string Numero { get; }

    public bool EhCelular => Numero.Length == 9;

    /// <summary><c>(11) 98765-4321</c> ou <c>(11) 3333-4444</c>.</summary>
    public string Formatado => $"({Ddd}) {Numero[..^4]}-{Numero[^4..]}";

    /// <summary>No formato internacional, como o WhatsApp e as centrais pedem: <c>+5511987654321</c>.</summary>
    public string Internacional => $"+55{Ddd}{Numero}";

    public static Telefone De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        if (!texto.All(caractere => char.IsAsciiDigit(caractere) || caractere is ' ' or '(' or ')' or '-' or '.' or '+'))
        {
            throw new ExcecaoDeDominio($"\"{texto.Trim()}\" não é um telefone: só dígitos, com parênteses, traço ou espaço.");
        }

        var digitos = string.Concat(texto.Where(char.IsAsciiDigit));

        if (digitos.Length == 11 && digitos.StartsWith('0') && digitos[2..4] == "00")
        {
            throw new ExcecaoDeDominio($"{texto.Trim()} é número de serviço, e não telefone de contato.");
        }

        // O código do país e o zero da operadora não fazem parte do número.
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal))
        {
            digitos = digitos[2..];
        }
        else if (digitos.Length is 11 or 12 && digitos.StartsWith('0'))
        {
            digitos = digitos[1..];
        }

        if (digitos.Length is not (10 or 11))
        {
            throw new ExcecaoDeDominio($"\"{texto.Trim()}\" não é um telefone: são o DDD e 8 dígitos no fixo, ou 9 no celular.");
        }

        var ddd = digitos[..2];
        var numero = digitos[2..];

        if (!Ddds.Contains(ddd))
        {
            throw new ExcecaoDeDominio($"O DDD {ddd} não existe.");
        }

        if (numero.Length == 9 ? numero[0] != '9' : numero[0] is < '2' or > '5')
        {
            throw new ExcecaoDeDominio(numero.Length == 9
                ? $"\"{texto.Trim()}\" não é um celular: o celular começa com 9."
                : $"\"{texto.Trim()}\" não é um telefone fixo: o fixo começa de 2 a 5.");
        }

        return new Telefone(ddd, numero);
    }

    public override string ToString() => Formatado;
}
