using System.Globalization;

namespace CrmObras.Dominio.Comum;

/// <summary>
/// Números escritos como se lê no Brasil — "2.430,50" —, para as mensagens e
/// o texto do domínio.
/// </summary>
/// <remarks>
/// Montado à mão, e não com <c>CultureInfo("pt-BR")</c>: o texto não pode
/// depender dos dados de cultura instalados na máquina que roda, nem da
/// cultura do processo.
/// </remarks>
internal static class FormatoBrasileiro
{
    public static readonly NumberFormatInfo Numeros = NumberFormatInfo.ReadOnly(new NumberFormatInfo
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NegativeSign = "-",
    });

    /// <summary>"2.430,50": duas casas no mínimo, e as que mais houver.</summary>
    public static string Numero(decimal valor) => valor.ToString("#,##0.00##########", Numeros);
}
