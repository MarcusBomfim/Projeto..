using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Propostas;

namespace CrmObras.Dominio.Testes.Propostas;

public sealed class AlcadasTests
{
    [Fact]
    public void A_padrao_e_5_10_e_15_por_cento()
    {
        var padrao = Alcadas.Padrao;

        Assert.Equal((5m, 10m, 15m), (padrao.De(Cargo.Vendedor), padrao.De(Cargo.Gerente), padrao.De(Cargo.Diretor)));
        Assert.Equal((5m, 10m, 15m), (padrao.Vendedor, padrao.Gerente, padrao.Diretor));
    }

    [Theory]
    [InlineData("0", Cargo.Vendedor)]
    [InlineData("5", Cargo.Vendedor)]
    [InlineData("5.01", Cargo.Gerente)]
    [InlineData("10", Cargo.Gerente)]
    [InlineData("10.01", Cargo.Diretor)]
    [InlineData("15", Cargo.Diretor)]
    public void Diz_o_cargo_mais_baixo_que_da_o_desconto(string desconto, Cargo cargo)
    {
        Assert.Equal(cargo, Alcadas.Padrao.QuemDa(decimal.Parse(desconto, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Acima_da_alcada_do_diretor_ninguem_da()
    {
        Assert.Null(Alcadas.Padrao.QuemDa(15.01m));
    }

    [Fact]
    public void Cargos_seguidos_podem_ter_a_mesma_alcada()
    {
        var alcadas = new Alcadas(0m, 8m, 8m);

        Assert.Equal(Cargo.Gerente, alcadas.QuemDa(8m));
        Assert.Equal(Cargo.Vendedor, alcadas.QuemDa(0m));
        Assert.Equal(new Alcadas(0m, 8m, 8m), alcadas);
    }

    [Fact]
    public void Quem_esta_acima_nao_tem_alcada_menor()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Alcadas(10m, 5m, 4m));

        Assert.Equal(
            "A alçada do gerente não pode ser menor que a do vendedor. A alçada do diretor não pode ser menor que a do gerente.",
            erro.Message);
    }

    [Fact]
    public void A_alcada_e_porcentagem_com_ate_duas_casas()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new Alcadas(-1m, 7.125m, 100m));

        Assert.Equal(
            "A alçada do vendedor vai de 0% a menos de 100%, com até duas casas: -1% não serve. "
            + "A alçada do gerente vai de 0% a menos de 100%, com até duas casas: 7,125% não serve. "
            + "A alçada do diretor vai de 0% a menos de 100%, com até duas casas: 100% não serve.",
            erro.Message);
        Assert.Equal(99.99m, new Alcadas(0m, 7.25m, 99.99m).Diretor);
    }
}
