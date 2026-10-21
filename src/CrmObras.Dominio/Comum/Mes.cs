using System.Globalization;

namespace CrmObras.Dominio.Comum;

/// <summary>Um mês do calendário: dezembro de 2026.</summary>
/// <remarks>
/// Record de classe, e não <c>DateOnly</c> no dia 1: "dezembro" não é o dia 1
/// de dezembro, e a previsão de vendas agrupa por mês sem que ninguém precise
/// lembrar de truncar a data.
/// </remarks>
public sealed record Mes : IComparable<Mes>
{
    private Mes(int ano, int numero)
    {
        Ano = ano;
        Numero = numero;
    }

    public int Ano { get; }

    /// <summary>De 1, janeiro, a 12, dezembro.</summary>
    public int Numero { get; }

    public DateOnly PrimeiroDia => new(Ano, Numero, 1);

    public DateOnly UltimoDia => new(Ano, Numero, DateTime.DaysInMonth(Ano, Numero));

    public static Mes De(int ano, int numero)
    {
        if (numero is < 1 or > 12)
        {
            throw new ExcecaoDeDominio($"O mês vai de 1 a 12: {numero} não é mês.");
        }

        if (ano is < 1 or > 9999)
        {
            throw new ExcecaoDeDominio($"O ano vai de 1 a 9999: {ano} não serve.");
        }

        return new Mes(ano, numero);
    }

    /// <summary>O mês em que a data cai.</summary>
    public static Mes De(DateOnly data) => new(data.Year, data.Month);

    /// <summary>O mês tantos meses depois — ou antes, se negativo.</summary>
    public Mes Mais(int meses) => De(PrimeiroDia.AddMonths(meses));

    public int CompareTo(Mes? other) => other is null ? 1 : (Ano, Numero).CompareTo((other.Ano, other.Numero));

    /// <summary>"12/2026".</summary>
    public override string ToString() => PrimeiroDia.ToString("MM/yyyy", CultureInfo.InvariantCulture);

    public static bool operator <(Mes esquerda, Mes direita) => Comparar(esquerda, direita) < 0;

    public static bool operator >(Mes esquerda, Mes direita) => Comparar(esquerda, direita) > 0;

    public static bool operator <=(Mes esquerda, Mes direita) => Comparar(esquerda, direita) <= 0;

    public static bool operator >=(Mes esquerda, Mes direita) => Comparar(esquerda, direita) >= 0;

    private static int Comparar(Mes esquerda, Mes direita)
    {
        ArgumentNullException.ThrowIfNull(esquerda);

        return esquerda.CompareTo(direita);
    }
}
