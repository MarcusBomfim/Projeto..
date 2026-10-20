using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Identificacao;

/// <summary>
/// O CNPJ de uma empresa — o numérico de sempre e o alfanumérico, que a
/// Receita emite desde julho de 2026.
/// </summary>
/// <remarks>
/// <para>
/// São 12 caracteres de base, letras maiúsculas ou dígitos, e 2 dígitos
/// verificadores, sempre numéricos. No alfanumérico, a conta dos
/// verificadores é a mesma de antes, com cada letra valendo o seu código
/// ASCII menos 48: o CNPJ numérico continua válido sem mudar nada.
/// </para>
/// <para>
/// Os 8 primeiros caracteres são a raiz: iguais na matriz e em todas as
/// filiais. A filial é outro CNPJ, mas não outra empresa.
/// </para>
/// </remarks>
public sealed record Cnpj : Documento
{
    private Cnpj(string numero)
        : base(numero)
    {
    }

    /// <summary>A raiz: os 8 primeiros caracteres, os mesmos na matriz e nas filiais.</summary>
    public string Raiz => Numero[..8];

    /// <summary>Se é a matriz: a ordem do estabelecimento é 0001.</summary>
    public bool EhMatriz => Numero[8..12] == "0001";

    public override string Formatado => $"{Numero[..2]}.{Numero[2..5]}.{Numero[5..8]}/{Numero[8..12]}-{Numero[12..]}";

    public static new Cnpj De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var numero = Limpar(texto);

        if (numero.Length != 14 || !numero[..12].All(char.IsAsciiLetterOrDigit) || !numero[12..].All(char.IsAsciiDigit))
        {
            throw new ExcecaoDeDominio(
                $"\"{texto.Trim()}\" não é um CNPJ: são 12 letras ou dígitos, e depois 2 dígitos verificadores.");
        }

        if (numero.All(caractere => caractere == '0')
            || DigitoVerificador(numero[..12], 9) != numero[12]
            || DigitoVerificador(numero[..13], 9) != numero[13])
        {
            throw new ExcecaoDeDominio($"O CNPJ {texto.Trim()} não confere: os dígitos verificadores não batem com o número.");
        }

        return new Cnpj(numero);
    }
}
