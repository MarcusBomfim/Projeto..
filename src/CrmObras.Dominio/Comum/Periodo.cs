namespace CrmObras.Dominio.Comum;

/// <summary>Os dias de um relatório, do primeiro ao último, inclusive, num fuso.</summary>
/// <remarks>
/// O fuso faz parte do período: a venda ganha às 22h de 31/12 em Brasília é
/// de 1º/1 em Greenwich. O relatório de quem está em Brasília a conta em
/// dezembro.
/// </remarks>
public sealed record Periodo
{
    private static readonly TimeSpan MaiorFuso = TimeSpan.FromHours(14);

    private Periodo(DateOnly primeiroDia, DateOnly ultimoDia, TimeSpan fuso)
    {
        PrimeiroDia = primeiroDia;
        UltimoDia = ultimoDia;
        Fuso = fuso;
    }

    public DateOnly PrimeiroDia { get; }

    public DateOnly UltimoDia { get; }

    /// <summary>A diferença para Greenwich: −3h em Brasília.</summary>
    public TimeSpan Fuso { get; }

    public static Periodo De(DateOnly primeiroDia, DateOnly ultimoDia, TimeSpan fuso)
    {
        if (ultimoDia < primeiroDia)
        {
            throw new ExcecaoDeDominio(
                $"O período termina em {FormatoBrasileiro.Data(ultimoDia)}, antes de começar, em {FormatoBrasileiro.Data(primeiroDia)}.");
        }

        if (fuso < -MaiorFuso || fuso > MaiorFuso || fuso.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new ExcecaoDeDominio("O fuso vai de −14h a +14h, em minutos inteiros.");
        }

        return new Periodo(primeiroDia, ultimoDia, fuso);
    }

    /// <summary>O mês inteiro.</summary>
    public static Periodo Do(Mes mes, TimeSpan fuso)
    {
        ArgumentNullException.ThrowIfNull(mes);

        return De(mes.PrimeiroDia, mes.UltimoDia, fuso);
    }

    /// <summary>Se o instante cai num dos dias do período, contado no fuso dele.</summary>
    public bool Contem(DateTimeOffset instante)
    {
        var dia = DateOnly.FromDateTime(instante.ToOffset(Fuso).DateTime);

        return dia >= PrimeiroDia && dia <= UltimoDia;
    }

    /// <summary>"01/10/2026 a 31/12/2026".</summary>
    public override string ToString() => $"{FormatoBrasileiro.Data(PrimeiroDia)} a {FormatoBrasileiro.Data(UltimoDia)}";
}
