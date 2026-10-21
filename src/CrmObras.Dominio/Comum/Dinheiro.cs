namespace CrmObras.Dominio.Comum;

/// <summary>
/// Um valor em reais.
/// </summary>
/// <remarks>
/// <para>
/// Sobre <c>decimal</c>, nunca <c>double</c>: em ponto flutuante binário,
/// 0,1 + 0,2 não dá 0,3 — e proposta que não fecha no centavo não passa em
/// auditoria.
/// </para>
/// <para>
/// É <c>record struct</c>, ao contrário do CNPJ e dos ids: aqui o valor padrão,
/// zero real, é legítimo. Não há regra que o <c>default</c> fure.
/// </para>
/// <para>
/// Guarda a precisão que a conta tiver. Arredondar é decisão de regra de
/// negócio, tomada em pontos definidos. E é uma moeda só: no dia em que houver
/// obra vendida em outra, a moeda entra como campo e somar real com dólar vira
/// erro. Até lá, seria complexidade sem uso.
/// </para>
/// </remarks>
public readonly record struct Dinheiro : IComparable<Dinheiro>
{
    public static readonly Dinheiro Zero;

    public Dinheiro(decimal valor) => Valor = valor;

    public decimal Valor { get; }

    /// <summary>Se o valor cabe em centavos: no máximo duas casas decimais.</summary>
    public bool EstaEmCentavos => Valor == decimal.Round(Valor, 2);

    public static Dinheiro Reais(decimal valor) => new(valor);

    /// <summary>
    /// Arredondado ao centavo pela NBR 5891: abaixo de meio centavo desce,
    /// acima sobe, e meio centavo exato vai para o centavo par.
    /// </summary>
    /// <remarks>
    /// Arredondar o meio sempre para cima puxa para cima a soma de muitas
    /// parcelas; para o par, os desvios se compensam. É também o padrão do
    /// <c>decimal</c> no .NET, mas fica escrito: a regra é da norma, e não
    /// pode mudar se o padrão do framework mudar.
    /// </remarks>
    public Dinheiro EmCentavos() => new(decimal.Round(Valor, 2, MidpointRounding.ToEven));

    public int CompareTo(Dinheiro other) => Valor.CompareTo(other.Valor);

    /// <summary>"R$ 1.234,56". Valores fora do centavo mostram todas as casas: "R$ 7,75272".</summary>
    public override string ToString()
    {
        var absoluto = FormatoBrasileiro.Numero(Math.Abs(Valor));

        return Valor < 0 ? $"-R$ {absoluto}" : $"R$ {absoluto}";
    }

    public static Dinheiro operator +(Dinheiro esquerda, Dinheiro direita) => new(esquerda.Valor + direita.Valor);

    public static Dinheiro operator -(Dinheiro esquerda, Dinheiro direita) => new(esquerda.Valor - direita.Valor);

    public static Dinheiro operator -(Dinheiro valor) => new(-valor.Valor);

    public static Dinheiro operator *(Dinheiro valor, decimal fator) => new(valor.Valor * fator);

    public static Dinheiro operator *(decimal fator, Dinheiro valor) => new(fator * valor.Valor);

    /// <summary>A razão entre dois valores: quanto o primeiro é do segundo.</summary>
    public static decimal operator /(Dinheiro parte, Dinheiro todo) => parte.Valor / todo.Valor;

    public static bool operator <(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor < direita.Valor;

    public static bool operator >(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor > direita.Valor;

    public static bool operator <=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor <= direita.Valor;

    public static bool operator >=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor >= direita.Valor;
}
