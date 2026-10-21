using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Propostas;

/// <summary>
/// Até quanto de desconto cada cargo dá sem pedir a ninguém (ADR-0007).
/// </summary>
/// <remarks>
/// Cada construtora define as suas; a padrão é 5% para o vendedor, 10% para o
/// gerente e 15% para o diretor. Quem está acima não tem alçada menor: o
/// gerente dá pelo menos o que o vendedor dá. Acima da alçada do diretor, não
/// há quem aprove.
/// </remarks>
public sealed record Alcadas
{
    public Alcadas(decimal vendedor, decimal gerente, decimal diretor)
    {
        var problemas = new[]
        {
            ProblemaDa(vendedor, Cargo.Vendedor),
            ProblemaDa(gerente, Cargo.Gerente),
            ProblemaDa(diretor, Cargo.Diretor),
            gerente < vendedor ? "A alçada do gerente não pode ser menor que a do vendedor." : null,
            diretor < gerente ? "A alçada do diretor não pode ser menor que a do gerente." : null,
        }.OfType<string>().ToList();

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Vendedor = vendedor;
        Gerente = gerente;
        Diretor = diretor;
    }

    public static Alcadas Padrao { get; } = new(5m, 10m, 15m);

    public decimal Vendedor { get; }

    public decimal Gerente { get; }

    public decimal Diretor { get; }

    /// <summary>Até quanto o cargo dá de desconto, em porcentagem.</summary>
    public decimal De(Cargo cargo) => cargo switch
    {
        Cargo.Vendedor => Vendedor,
        Cargo.Gerente => Gerente,
        Cargo.Diretor => Diretor,
        _ => throw new ArgumentOutOfRangeException(nameof(cargo), cargo, null),
    };

    /// <summary>O cargo mais baixo que dá o desconto; nulo se nem o diretor dá.</summary>
    public Cargo? QuemDa(decimal desconto) =>
        desconto <= Vendedor ? Cargo.Vendedor
        : desconto <= Gerente ? Cargo.Gerente
        : desconto <= Diretor ? Cargo.Diretor
        : null;

    /// <summary>"vendedor", "gerente", "diretor": como o cargo entra numa frase.</summary>
    internal static string Nome(Cargo cargo) => cargo.ToString().ToLowerInvariant();

    /// <summary>De 0 a menos de 100%, com até duas casas.</summary>
    internal static bool EhPorcentagem(decimal valor) => valor is >= 0 and < 100 && valor == decimal.Round(valor, 2);

    private static string? ProblemaDa(decimal alcada, Cargo cargo) =>
        EhPorcentagem(alcada)
            ? null
            : $"A alçada do {Nome(cargo)} vai de 0% a menos de 100%, com até duas casas: {FormatoBrasileiro.Porcentagem(alcada)} não serve.";
}
