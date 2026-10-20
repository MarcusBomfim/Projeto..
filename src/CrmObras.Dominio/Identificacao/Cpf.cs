using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Identificacao;

/// <summary>O CPF de uma pessoa: 9 dígitos de base e 2 verificadores.</summary>
/// <remarks>
/// Os onze dígitos iguais — 111.111.111-11 e os outros — passam na conta dos
/// verificadores, mas a Receita não os emite: são recusados.
/// </remarks>
public sealed record Cpf : Documento
{
    private Cpf(string numero)
        : base(numero)
    {
    }

    public override string Formatado => $"{Numero[..3]}.{Numero[3..6]}.{Numero[6..9]}-{Numero[9..]}";

    public static new Cpf De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var numero = Limpar(texto);

        if (numero.Length != 11 || !numero.All(char.IsAsciiDigit))
        {
            throw new ExcecaoDeDominio($"\"{texto.Trim()}\" não é um CPF: são 11 dígitos.");
        }

        if (numero.All(digito => digito == numero[0])
            || DigitoVerificador(numero[..9], 11) != numero[9]
            || DigitoVerificador(numero[..10], 11) != numero[10])
        {
            throw new ExcecaoDeDominio($"O CPF {texto.Trim()} não confere: os dígitos verificadores não batem com o número.");
        }

        return new Cpf(numero);
    }
}
