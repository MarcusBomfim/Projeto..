using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Testes.Comum;

public sealed class PeriodoTests
{
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    [Fact]
    public void Vai_do_primeiro_ao_ultimo_dia_inclusive()
    {
        var periodo = Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), Brasilia);

        Assert.Equal("01/10/2026 a 31/12/2026", periodo.ToString());
        Assert.True(periodo.Contem(new DateTimeOffset(2026, 10, 1, 0, 0, 0, Brasilia)));
        Assert.True(periodo.Contem(new DateTimeOffset(2026, 12, 31, 23, 59, 59, Brasilia)));
        Assert.False(periodo.Contem(new DateTimeOffset(2026, 9, 30, 23, 59, 59, Brasilia)));
        Assert.False(periodo.Contem(new DateTimeOffset(2027, 1, 1, 0, 0, 0, Brasilia)));
    }

    [Fact]
    public void Conta_o_dia_no_fuso_do_periodo()
    {
        var dezembroEmBrasilia = Periodo.Do(Mes.De(2026, 12), Brasilia);
        var dezembroEmGreenwich = Periodo.Do(Mes.De(2026, 12), TimeSpan.Zero);
        var anoNovoEmGreenwich = new DateTimeOffset(2027, 1, 1, 1, 0, 0, TimeSpan.Zero);

        Assert.True(dezembroEmBrasilia.Contem(anoNovoEmGreenwich));
        Assert.False(dezembroEmGreenwich.Contem(anoNovoEmGreenwich));
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), Brasilia), (dezembroEmBrasilia.PrimeiroDia, dezembroEmBrasilia.UltimoDia, dezembroEmBrasilia.Fuso));
    }

    [Fact]
    public void Um_dia_so_e_periodo()
    {
        var dia = Periodo.De(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), Brasilia);

        Assert.True(dia.Contem(new DateTimeOffset(2026, 10, 21, 12, 0, 0, Brasilia)));
    }

    [Fact]
    public void Nao_termina_antes_de_comecar()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Periodo.De(new DateOnly(2026, 12, 31), new DateOnly(2026, 10, 1), Brasilia));
        var naVespera = Assert.Throws<ExcecaoDeDominio>(() => Periodo.De(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 20), Brasilia));

        Assert.Equal("O período termina em 01/10/2026, antes de começar, em 31/12/2026.", erro.Message);
        Assert.Equal("O período termina em 20/10/2026, antes de começar, em 21/10/2026.", naVespera.Message);
    }

    [Theory]
    [InlineData(-14 * 60 - 1)]
    [InlineData(14 * 60 + 1)]
    public void O_fuso_vai_de_menos_14_a_mais_14_horas(int minutos)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), TimeSpan.FromMinutes(minutos)));

        Assert.Equal("O fuso vai de −14h a +14h, em minutos inteiros.", erro.Message);
    }

    [Fact]
    public void O_fuso_e_em_minutos_inteiros()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), TimeSpan.FromSeconds(-3 * 3600 + 30)));

        Assert.Equal("O fuso vai de −14h a +14h, em minutos inteiros.", erro.Message);
        Assert.Equal(TimeSpan.FromHours(14), Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), TimeSpan.FromHours(14)).Fuso);
        Assert.Equal(TimeSpan.FromHours(-14), Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), TimeSpan.FromHours(-14)).Fuso);
        Assert.Equal(TimeSpan.FromMinutes(345), Periodo.De(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), TimeSpan.FromMinutes(345)).Fuso);
    }
}
