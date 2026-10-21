using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Testes.Comum;

public sealed class DinheiroTests
{
    [Fact]
    public void Soma_sem_erro_de_ponto_flutuante()
    {
        // Em double, 0,1 + 0,2 dá 0,30000000000000004.
        Assert.Equal(Dinheiro.Reais(0.3m), Dinheiro.Reais(0.1m) + Dinheiro.Reais(0.2m));
    }

    [Theory]
    //          valor     arredondado
    [InlineData("3.085", "3.08")] // meio centavo exato, 8 é par: fica
    [InlineData("3.075", "3.08")] // meio centavo exato, 7 é ímpar: sobe para o par
    [InlineData("3.0851", "3.09")] // passou do meio: sobe
    [InlineData("3.0849", "3.08")] // não chegou ao meio: desce
    [InlineData("0.015", "0.02")]
    [InlineData("0.025", "0.02")]
    [InlineData("-3.085", "-3.08")]
    public void Arredonda_ao_centavo_pela_NBR_5891(string valor, string esperado)
    {
        var arredondado = Dinheiro.Reais(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)).EmCentavos();

        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), arredondado.Valor);
    }

    [Fact]
    public void Sabe_se_ja_esta_em_centavos()
    {
        Assert.True(Dinheiro.Reais(1.5m).EstaEmCentavos);
        Assert.True(Dinheiro.Reais(1.5000m).EstaEmCentavos);
        Assert.False(Dinheiro.Reais(1.505m).EstaEmCentavos);
    }

    [Fact]
    public void Zeros_a_direita_nao_mudam_o_valor()
    {
        Assert.Equal(Dinheiro.Reais(1.5m), Dinheiro.Reais(1.50m));
        Assert.Equal(Dinheiro.Reais(1.5m).GetHashCode(), Dinheiro.Reais(1.50m).GetHashCode());
    }

    [Fact]
    public void O_padrao_e_zero_real()
    {
        Assert.Equal(Dinheiro.Zero, default);
        Assert.Equal(0m, Dinheiro.Zero.Valor);
    }

    [Theory]
    [InlineData("1234.56", "R$ 1.234,56")]
    [InlineData("1234567.8", "R$ 1.234.567,80")]
    [InlineData("0", "R$ 0,00")]
    [InlineData("-12.5", "-R$ 12,50")]
    [InlineData("7.75272", "R$ 7,75272")]
    public void Escreve_como_se_le_no_brasil(string valor, string esperado)
    {
        var dinheiro = Dinheiro.Reais(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(esperado, dinheiro.ToString());
    }

    [Fact]
    public void Compara_multiplica_e_divide()
    {
        var cem = Dinheiro.Reais(100m);
        var vinte = Dinheiro.Reais(20m);

        Assert.True(vinte < cem);
        Assert.True(cem >= vinte);
        Assert.Equal(-1, vinte.CompareTo(cem));
        Assert.Equal(Dinheiro.Reais(125m), cem * 1.25m);
        Assert.Equal(Dinheiro.Reais(125m), 1.25m * cem);
        Assert.Equal(Dinheiro.Reais(80m), cem - vinte);
        Assert.Equal(Dinheiro.Reais(-20m), -vinte);
        Assert.Equal(0.2m, vinte / cem);
    }
}
