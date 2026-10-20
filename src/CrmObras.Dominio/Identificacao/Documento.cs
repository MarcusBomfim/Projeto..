using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Identificacao;

/// <summary>
/// O documento que identifica o cliente perante a Receita: o CNPJ da empresa
/// ou o CPF da pessoa.
/// </summary>
/// <remarks>
/// Guardado sem máscara e mostrado com ela. Dois documentos são iguais se o
/// tipo e o número são iguais: um CPF nunca é igual a um CNPJ, mesmo que os
/// dígitos coincidissem.
/// </remarks>
public abstract record Documento
{
    private protected Documento(string numero) => Numero = numero;

    /// <summary>Sem máscara: <c>11222333000181</c>, <c>52998224725</c>, <c>12ABC34501DE35</c>.</summary>
    public string Numero { get; }

    /// <summary>Com a máscara de costume: <c>11.222.333/0001-81</c>, <c>529.982.247-25</c>.</summary>
    public abstract string Formatado { get; }

    /// <summary>
    /// Lê um CNPJ ou um CPF, com ou sem máscara, decidindo pelo tamanho: 14
    /// caracteres é CNPJ, 11 é CPF.
    /// </summary>
    public static Documento De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Limpar(texto).Length switch
        {
            14 => Cnpj.De(texto),
            11 => Cpf.De(texto),
            _ => throw new ExcecaoDeDominio(
                $"\"{texto.Trim()}\" não é CPF nem CNPJ: o CPF tem 11 dígitos, e o CNPJ, 14 caracteres."),
        };
    }

    public sealed override string ToString() => Formatado;

    /// <summary>Tira a máscara — pontos, barra, traço e espaços — e põe as letras em maiúscula.</summary>
    private protected static string Limpar(string texto) =>
        string.Concat(texto.Where(caractere => caractere is not ('.' or '/' or '-' or ' '))).ToUpperInvariant();

    /// <summary>
    /// O dígito verificador por módulo 11, com os pesos de 2 a 9 da direita
    /// para a esquerda — a regra do CPF e do CNPJ. Cada caractere vale o seu
    /// código ASCII menos 48: os dígitos valem o que são, e as letras do CNPJ
    /// alfanumérico, de 17 (A) a 42 (Z).
    /// </summary>
    private protected static char DigitoVerificador(string caracteres, int maiorPeso)
    {
        var soma = 0;
        var peso = 2;

        for (var posicao = caracteres.Length - 1; posicao >= 0; posicao--)
        {
            soma += (caracteres[posicao] - '0') * peso;
            peso = peso == maiorPeso ? 2 : peso + 1;
        }

        var resto = soma % 11;

        return resto < 2 ? '0' : (char)('0' + (11 - resto));
    }
}
