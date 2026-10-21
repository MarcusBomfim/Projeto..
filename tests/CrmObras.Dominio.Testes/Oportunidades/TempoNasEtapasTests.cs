using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

public sealed class TempoNasEtapasTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Soma_o_tempo_de_cada_etapa_e_quem_volta_soma_de_novo()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        var orcamento = _funil.Chamada("Orçamento em elaboração");
        var proposta = _funil.Chamada("Proposta enviada");

        oportunidade.MoverPara(_funil, orcamento.Id, Segunda.AddDays(2));
        oportunidade.MoverPara(_funil, proposta.Id, Segunda.AddDays(12));
        oportunidade.MoverPara(_funil, orcamento.Id, Segunda.AddDays(15));

        Assert.Equal(
            [
                new TempoNaEtapa(_funil.Primeira.Id, TimeSpan.FromDays(2)),
                new TempoNaEtapa(orcamento.Id, TimeSpan.FromDays(10 + 6)),
                new TempoNaEtapa(proposta.Id, TimeSpan.FromDays(3)),
            ],
            oportunidade.TempoEmCadaEtapa(Segunda.AddDays(21)));
        Assert.Equal(TimeSpan.FromDays(6), oportunidade.TempoNaEtapaAtual(Segunda.AddDays(21)));
    }

    [Fact]
    public void Fechada_a_venda_o_relogio_para()
    {
        var oportunidade = Completa(_funil, _rotaSul);
        oportunidade.MoverPara(_funil, _funil.Chamada("Negociação").Id, Segunda.AddDays(4));
        oportunidade.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Segunda.AddDays(10));

        var tempos = oportunidade.TempoEmCadaEtapa(Segunda.AddDays(400));

        Assert.Equal([TimeSpan.FromDays(4), TimeSpan.FromDays(6)], tempos.Select(tempo => tempo.Tempo));
        Assert.DoesNotContain(tempos, tempo => tempo.Etapa == _funil.Ganha.Id);
        Assert.Equal(TimeSpan.FromDays(390), oportunidade.TempoNaEtapaAtual(Segunda.AddDays(400)));
    }

    [Fact]
    public void O_tempo_perdida_conta_depois_que_a_venda_e_reaberta()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.ProjetoCancelado), Segunda.AddDays(5));
        Assert.Equal([new TempoNaEtapa(_funil.Primeira.Id, TimeSpan.FromDays(5))], oportunidade.TempoEmCadaEtapa(Segunda.AddDays(50)));

        oportunidade.Reabrir(_funil, _funil.Primeira.Id, Segunda.AddDays(35));

        Assert.Equal(
            [
                new TempoNaEtapa(_funil.Primeira.Id, TimeSpan.FromDays(5 + 15)),
                new TempoNaEtapa(_funil.Perdida.Id, TimeSpan.FromDays(30)),
            ],
            oportunidade.TempoEmCadaEtapa(Segunda.AddDays(50)));
    }

    [Fact]
    public void Esta_parada_quando_passa_dos_dias_que_a_etapa_espera()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);

        Assert.False(oportunidade.EstaParada(_funil, Segunda.AddDays(7)));
        Assert.True(oportunidade.EstaParada(_funil, Segunda.AddDays(7).AddMinutes(1)));
    }

    [Fact]
    public void Etapa_sem_prazo_nunca_deixa_a_venda_parada()
    {
        var funil = new Funil("Balcão", [new("Atendimento", 50)]);
        var oportunidade = new Oportunidade(_rotaSul.Id, "Reforma da recepção", funil, "carlos.souza", Segunda);

        Assert.False(oportunidade.EstaParada(funil, Segunda.AddYears(3)));
    }

    [Fact]
    public void Venda_fechada_nao_esta_parada()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        oportunidade.Perder(_funil, new Perda(MotivoDaPerda.SemResposta), Segunda.AddDays(1));

        Assert.False(oportunidade.EstaParada(_funil, Segunda.AddDays(90)));
    }

    [Fact]
    public void O_prazo_e_o_da_etapa_hoje_ainda_que_tenha_mudado_depois_da_entrada()
    {
        var oportunidade = DoGalpao(_funil, _rotaSul);
        var contato = _funil.Primeira;

        _funil.AlterarEtapa(contato.Id, new(contato.Nome, contato.Probabilidade, 30));

        Assert.False(oportunidade.EstaParada(_funil, Segunda.AddDays(20)));
    }
}
