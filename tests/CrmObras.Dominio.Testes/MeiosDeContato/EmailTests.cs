using CrmObras.Dominio.Comum;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes.MeiosDeContato;

public sealed class EmailTests
{
    [Theory]
    [InlineData("joao@alfa.com.br", "joao@alfa.com.br")]
    [InlineData("  Joao.Silva@Alfa.COM.br ", "joao.silva@alfa.com.br")]
    [InlineData("compras+obras@construtora-alfa.com", "compras+obras@construtora-alfa.com")]
    [InlineData("o'neil@empresa.io", "o'neil@empresa.io")]
    public void Guarda_em_minusculas_e_sem_espacos_nas_pontas(string texto, string endereco)
    {
        Assert.Equal(endereco, Email.De(texto).Endereco);
    }

    [Fact]
    public void O_mesmo_endereco_escrito_de_outro_jeito_e_o_mesmo_email()
    {
        Assert.Equal(Email.De("joao@alfa.com.br"), Email.De("JOAO@Alfa.com.br"));
        Assert.Equal("alfa.com.br", Email.De("joao@alfa.com.br").Dominio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("joao")]
    [InlineData("joao@")]
    [InlineData("@alfa.com.br")]
    [InlineData("joao@@alfa.com.br")]
    [InlineData("joao@alfa")]
    [InlineData("joao silva@alfa.com.br")]
    [InlineData("joao@alfa..com.br")]
    [InlineData("joao.@alfa.com.br")]
    [InlineData(".joao@alfa.com.br")]
    [InlineData("jo..ao@alfa.com.br")]
    [InlineData("joao@-alfa.com.br")]
    [InlineData("joao@alfa.com.b")]
    [InlineData("joao@alfa.com.2")]
    [InlineData("joao@alfa.com.22")]
    [InlineData("joão@alfa.com.br")]
    public void Recusa_o_que_nao_e_email(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Email.De(texto));

        Assert.Contains("não é um e-mail", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recusa_nome_antes_da_arroba_com_mais_de_64_caracteres()
    {
        Assert.Throws<ExcecaoDeDominio>(() => Email.De($"{new string('a', 65)}@alfa.com.br"));
        Assert.Equal(64, Email.De($"{new string('a', 64)}@alfa.com.br").Endereco.IndexOf('@', StringComparison.Ordinal));
    }
}
