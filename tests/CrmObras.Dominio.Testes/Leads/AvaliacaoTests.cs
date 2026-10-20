using CrmObras.Dominio.Leads;
using static CrmObras.Dominio.Leads.Resposta;

namespace CrmObras.Dominio.Testes.Leads;

public sealed class AvaliacaoTests
{
    [Theory]
    [InlineData(Sim, Sim, Sim, NaoSabido)]
    [InlineData(Sim, NaoSabido, Sim, Sim)]
    [InlineData(Sim, Nao, Sim, Sim)]
    [InlineData(Sim, Sim, Sim, Nao)]
    [InlineData(Sim, Sim, Sim, Sim)]
    public void Qualifica_com_necessidade_e_orcamento_e_quem_decide_ou_prazo(
        Resposta orcamento, Resposta quemDecide, Resposta necessidade, Resposta prazo)
    {
        var avaliacao = new Avaliacao(orcamento, quemDecide, necessidade, prazo);

        Assert.True(avaliacao.Qualifica);
        Assert.Empty(avaliacao.Pendencias);
    }

    [Theory]
    [InlineData(NaoSabido, Sim, Sim, Sim, "o orçamento confirmado")]
    [InlineData(Nao, Sim, Sim, Sim, "o orçamento confirmado")]
    [InlineData(Sim, Sim, NaoSabido, Sim, "a necessidade da obra confirmada")]
    [InlineData(Sim, Nao, Sim, NaoSabido, "falar com quem decide ou ter o prazo da obra")]
    [InlineData(Sim, NaoSabido, Sim, Nao, "falar com quem decide ou ter o prazo da obra")]
    public void Diz_o_que_falta(Resposta orcamento, Resposta quemDecide, Resposta necessidade, Resposta prazo, string pendencia)
    {
        var avaliacao = new Avaliacao(orcamento, quemDecide, necessidade, prazo);

        Assert.False(avaliacao.Qualifica);
        Assert.Equal([pendencia], avaliacao.Pendencias);
    }

    [Fact]
    public void A_avaliacao_que_ninguem_fez_tem_tudo_pendente()
    {
        Assert.Equal(
            ["a necessidade da obra confirmada", "o orçamento confirmado", "falar com quem decide ou ter o prazo da obra"],
            Avaliacao.NaoFeita.Pendencias);
    }
}
