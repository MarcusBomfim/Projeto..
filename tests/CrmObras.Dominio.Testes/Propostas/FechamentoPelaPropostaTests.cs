using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Oportunidades;
using static CrmObras.Dominio.Testes.Propostas.PropostasDeTeste;

namespace CrmObras.Dominio.Testes.Propostas;

public sealed class FechamentoPelaPropostaTests
{
    private static readonly DateTimeOffset Aceite = Segunda.AddDays(10);

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Oportunidade _galpao;

    private readonly Proposta _proposta;

    public FechamentoPelaPropostaTests()
    {
        _galpao = VendasDeTeste.DoGalpao(_funil, VendasDeTeste.RotaSul());
        _proposta = DoGalpao(_galpao, 7.5m);
        _proposta.AprovarDesconto(Helena, Alcadas.Padrao, Segunda);
        _proposta.Enviar(Carlos, Alcadas.Padrao, Segunda.AddDays(1));
    }

    [Fact]
    public void A_proposta_aceita_ganha_a_venda_pelo_total_dela()
    {
        FechamentoPelaProposta.Aceitar(_proposta, _galpao, _funil, Aceite);

        Assert.Same(_proposta.Atual, _proposta.Aceita);
        Assert.Equal(
            (SituacaoDaOportunidade.Ganha, (Dinheiro?)Dinheiro.Reais(1_937_264.50m), (DateTimeOffset?)Aceite),
            (_galpao.Situacao, _galpao.ValorFechado, _galpao.FechadaEm));
    }

    [Fact]
    public void Proposta_de_outra_venda_nao_fecha_esta()
    {
        var outra = VendasDeTeste.DoGalpao(_funil, VendasDeTeste.RotaSul());
        outra.Renomear("Galpão de Sorocaba");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => FechamentoPelaProposta.Aceitar(_proposta, outra, _funil, Aceite));

        Assert.Equal("A proposta não é da oportunidade Galpão de Sorocaba.", erro.Message);
        Assert.Equal((SituacaoDaOportunidade.Aberta, (VersaoDaProposta?)null), (outra.Situacao, _proposta.Aceita));
    }

    [Fact]
    public void Proposta_vencida_nao_ganha_a_venda()
    {
        var vencida = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(-3));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => FechamentoPelaProposta.Aceitar(_proposta, _galpao, _funil, vencida));

        Assert.Equal("A versão 1 venceu em 30/11/2026: para o cliente aceitar, envie uma versão nova.", erro.Message);
        Assert.Equal(SituacaoDaOportunidade.Aberta, _galpao.Situacao);
    }

    [Fact]
    public void Venda_perdida_nao_aceita_a_proposta()
    {
        _galpao.Perder(_funil, new Perda(MotivoDaPerda.SemResposta), Segunda.AddDays(5));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => FechamentoPelaProposta.Aceitar(_proposta, _galpao, _funil, Aceite));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí foi perdida: reabra antes de mudá-la.", erro.Message);
        Assert.Null(_proposta.Aceita);
        Assert.Same(_proposta.Atual, _proposta.Vigente);
    }

    [Fact]
    public void Funil_errado_nao_aceita_a_proposta()
    {
        var outro = Funil.PadraoDaConstrutora();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => FechamentoPelaProposta.Aceitar(_proposta, _galpao, outro, Aceite));

        Assert.Equal("A oportunidade Galpão de 1.200 m² em Jundiaí não anda pelo funil Vendas de obras.", erro.Message);
        Assert.Null(_proposta.Aceita);
    }
}
