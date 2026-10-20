using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.MeiosDeContato;

/// <summary>Um endereço de e-mail, guardado em minúsculas.</summary>
/// <remarks>
/// <para>
/// Em minúsculas inteiro, e não só o domínio: a norma deixa a parte antes da
/// arroba diferenciar maiúsculas, mas nenhum provedor que se usa no Brasil
/// faz isso. É o que deixa <c>Joao@Alfa.com.br</c> e <c>joao@alfa.com.br</c>
/// serem o mesmo contato — e o mesmo duplicado.
/// </para>
/// <para>
/// A validação é a de quem preenche um cadastro: uma arroba, um nome antes, um
/// domínio com ponto depois, sem espaço. Se o endereço existe, só um e-mail
/// enviado responde.
/// </para>
/// </remarks>
public sealed record Email
{
    private const string PermitidosAntesDaArroba = "!#$%&'*+/=?^_`{|}~.-";

    private Email(string endereco) => Endereco = endereco;

    public string Endereco { get; }

    /// <summary>O que vem depois da arroba: <c>alfa.com.br</c>.</summary>
    public string Dominio => Endereco[(Endereco.IndexOf('@', StringComparison.Ordinal) + 1)..];

    public static Email De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        var endereco = texto.Trim().ToLowerInvariant();
        var partes = endereco.Split('@');

        if (endereco.Length > 254 || partes.Length != 2 || !NomeValido(partes[0]) || !DominioValido(partes[1]))
        {
            throw new ExcecaoDeDominio($"\"{texto.Trim()}\" não é um e-mail: falta o nome, a arroba ou um domínio como empresa.com.br.");
        }

        return new Email(endereco);
    }

    public override string ToString() => Endereco;

    private static bool NomeValido(string nome) =>
        nome.Length is > 0 and <= 64
        && nome.All(caractere => char.IsAsciiLetterOrDigit(caractere) || PermitidosAntesDaArroba.Contains(caractere, StringComparison.Ordinal))
        && !nome.StartsWith('.') && !nome.EndsWith('.') && !nome.Contains("..", StringComparison.Ordinal);

    private static bool DominioValido(string dominio)
    {
        var rotulos = dominio.Split('.');

        return rotulos.Length >= 2
            && rotulos.All(rotulo => rotulo.Length is > 0 and <= 63
                && rotulo.All(caractere => char.IsAsciiLetterOrDigit(caractere) || caractere == '-')
                && !rotulo.StartsWith('-') && !rotulo.EndsWith('-'))
            && rotulos[^1].Length >= 2 && rotulos[^1].All(char.IsAsciiLetter);
    }
}
