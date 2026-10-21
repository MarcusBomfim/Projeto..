using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Testes.Comum;

public sealed class MesTests
{
    [Fact]
    public void E_o_mes_em_que_a_data_cai()
    {
        var dezembro = Mes.De(new DateOnly(2026, 12, 15));

        Assert.Equal((2026, 12, "12/2026"), (dezembro.Ano, dezembro.Numero, dezembro.ToString()));
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), (dezembro.PrimeiroDia, dezembro.UltimoDia));
        Assert.Equal(Mes.De(2026, 12), dezembro);
    }

    [Fact]
    public void Sabe_o_ultimo_dia_de_fevereiro()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), Mes.De(2028, 2).UltimoDia);
        Assert.Equal(new DateOnly(2027, 2, 28), Mes.De(2027, 2).UltimoDia);
        Assert.Equal(new DateOnly(9999, 12, 31), Mes.De(9999, 12).UltimoDia);
    }

    [Fact]
    public void Anda_para_a_frente_e_para_tras_atravessando_o_ano()
    {
        var novembro = Mes.De(2026, 11);

        Assert.Equal(Mes.De(2027, 2), novembro.Mais(3));
        Assert.Equal(Mes.De(2025, 11), novembro.Mais(-12));
        Assert.Equal(novembro, novembro.Mais(0));
    }

    [Fact]
    public void Compara_pela_ordem_do_calendario()
    {
        var dezembro = Mes.De(2026, 12);
        var janeiro = Mes.De(2027, 1);

        Assert.True(dezembro < janeiro);
        Assert.True(janeiro > dezembro);
        Assert.True(dezembro <= Mes.De(2026, 12));
        Assert.True(janeiro >= Mes.De(2027, 1));
        Assert.False(janeiro <= dezembro);
        Assert.Equal(1, dezembro.CompareTo(null));
        Assert.Equal([Mes.De(2026, 2), Mes.De(2026, 11), dezembro, janeiro], new[] { janeiro, Mes.De(2026, 11), dezembro, Mes.De(2026, 2) }.Order());
    }

    [Theory]
    [InlineData(2026, 0, "O mês vai de 1 a 12: 0 não é mês.")]
    [InlineData(2026, 13, "O mês vai de 1 a 12: 13 não é mês.")]
    [InlineData(0, 1, "O ano vai de 1 a 9999: 0 não serve.")]
    [InlineData(10000, 1, "O ano vai de 1 a 9999: 10000 não serve.")]
    public void Recusa_o_que_nao_e_mes(int ano, int numero, string mensagem)
    {
        Assert.Equal(mensagem, Assert.Throws<ExcecaoDeDominio>(() => Mes.De(ano, numero)).Message);
    }

    [Fact]
    public void Aceita_os_limites()
    {
        Assert.Equal("01/0001", Mes.De(1, 1).ToString());
        Assert.Equal("12/9999", Mes.De(9999, 12).ToString());
    }
}
