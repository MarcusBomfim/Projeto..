using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;

namespace CrmObras.Dominio.Testes.Identificacao;

public sealed class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("111.444.777-35", "11144477735")]
    [InlineData("100.000.001-08", "10000000108")]
    public void Le_com_ou_sem_mascara(string texto, string numero)
    {
        var cpf = Cpf.De(texto);

        Assert.Equal((numero, $"{numero[..3]}.{numero[3..6]}.{numero[6..9]}-{numero[9..]}"), (cpf.Numero, cpf.Formatado));
    }

    [Theory]
    [InlineData("529.982.247-24")]
    [InlineData("529.982.247-35")]
    [InlineData("111.111.111-11")]
    [InlineData("000.000.000-00")]
    public void Recusa_o_que_nao_confere_e_os_digitos_todos_iguais(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Cpf.De(texto));

        Assert.Contains("não confere", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("5299822472")]
    [InlineData("529.982.247-2A")]
    public void Recusa_o_que_nao_tem_forma_de_cpf(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Cpf.De(texto));

        Assert.Contains("não é um CPF", erro.Message, StringComparison.Ordinal);
    }
}
