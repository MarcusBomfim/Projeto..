using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;

namespace CrmObras.Dominio.Testes.Identificacao;

public sealed class CnpjTests
{
    [Theory]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData("11222333000181", "11222333000181")]
    [InlineData(" 11 222 333 0001 81 ", "11222333000181")]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    [InlineData("AB12CD34000184", "AB12CD34000184")]
    public void Le_o_numerico_e_o_alfanumerico_com_ou_sem_mascara(string texto, string numero)
    {
        var cnpj = Cnpj.De(texto);

        Assert.Equal(numero, cnpj.Numero);
    }

    [Fact]
    public void Mostra_com_a_mascara_de_costume()
    {
        Assert.Equal("11.222.333/0001-81", Cnpj.De("11222333000181").Formatado);
        Assert.Equal("12.ABC.345/01DE-35", Cnpj.De("12abc34501de35").ToString());
    }

    [Theory]
    [InlineData("11.222.333/0001-82")]
    [InlineData("11.222.333/0001-91")]
    [InlineData("12.ABC.345/01DE-36")]
    [InlineData("00.000.000/0000-00")]
    public void Recusa_o_que_nao_confere_nos_digitos_verificadores(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Cnpj.De(texto));

        Assert.Contains("não confere", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1122233300018")]
    [InlineData("112223330001811")]
    [InlineData("11.222.333/0001-8A")]
    [InlineData("11.222.333/0001_81")]
    [InlineData("11.222.333/0001-8Ç")]
    public void Recusa_o_que_nao_tem_forma_de_cnpj(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Cnpj.De(texto));

        Assert.Contains("não é um CNPJ", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_filial_tem_a_raiz_da_matriz()
    {
        var matriz = Cnpj.De("11.222.333/0001-81");
        var filial = Cnpj.De("11.222.333/0002-62");

        Assert.Equal(("11222333", true), (matriz.Raiz, matriz.EhMatriz));
        Assert.Equal(("11222333", false), (filial.Raiz, filial.EhMatriz));
        Assert.NotEqual(matriz, filial);
    }
}
