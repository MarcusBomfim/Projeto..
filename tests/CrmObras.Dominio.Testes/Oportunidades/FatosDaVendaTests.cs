using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Propostas;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Oportunidades;

/*
 * O fato que a venda publica (ADR-0016): ganha, e só ganha, ela registra a
 * VendaGanha — com o que era verdade naquele instante —, pelo caminho que
 * for: direto, ou pelo aceite da proposta.
 */
public sealed class FatosDaVendaTests
{
    private static readonly DateTimeOffset Ganho = Segunda.AddDays(20);

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    [Fact]
    public void Ganha_registra_a_venda_ganha_com_a_obra_o_cliente_e_o_valor()
    {
        var venda = DoGalpao(_funil, _rotaSul);

        venda.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Ganho);

        Assert.Equal(
            [new VendaGanha(venda.Id, _rotaSul.Id, "Galpão de 1.200 m² em Jundiaí", Dinheiro.Reais(2_350_000m), Ganho)],
            venda.Eventos);
        Assert.Equal(venda.Id, venda.Eventos[0].Agregado);
    }

    [Fact]
    public void O_fato_e_do_instante_do_ganho_e_nao_muda_com_a_venda()
    {
        var venda = DoGalpao(_funil, _rotaSul);
        venda.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Ganho);

        venda.Renomear("Galpão logístico de Jundiaí");
        venda.TransferirPara("helena.martins");

        Assert.Equal("Galpão de 1.200 m² em Jundiaí", Assert.IsType<VendaGanha>(Assert.Single(venda.Eventos)).Titulo);
    }

    [Fact]
    public void O_aceite_da_proposta_registra_a_venda_ganha_pelo_total_dela()
    {
        var venda = DoGalpao(_funil, _rotaSul);
        var proposta = PropostasDeTeste.DoGalpao(venda);
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Segunda.AddDays(1));

        FechamentoPelaProposta.Aceitar(proposta, venda, _funil, Ganho);

        var ganha = Assert.IsType<VendaGanha>(Assert.Single(venda.Eventos));
        Assert.Equal((proposta.Aceita!.Total, Ganho), (ganha.ValorFechado, ganha.GanhaEm));
    }

    [Fact]
    public void O_resto_da_vida_da_venda_nao_e_fato_publicado()
    {
        var venda = Completa(_funil, _rotaSul);

        venda.MoverPara(_funil, _funil.Chamada("Negociação").Id, Segunda.AddDays(5));
        venda.Estimar(Dinheiro.Reais(2_500_000m));
        venda.Renomear("Galpão logístico de Jundiaí");
        venda.Perder(_funil, new Perda(MotivoDaPerda.Preco), Segunda.AddDays(6));
        venda.Reabrir(_funil, _funil.Primeira.Id, Segunda.AddDays(7));

        Assert.Empty(venda.Eventos);
    }

    [Fact]
    public void Ganho_recusado_nao_registra_nada()
    {
        var venda = DoGalpao(_funil, _rotaSul);

        Assert.Throws<ExcecaoDeDominio>(() => venda.Ganhar(_funil, Dinheiro.Reais(0m), Ganho));

        Assert.Empty(venda.Eventos);
    }

    [Fact]
    public void Descartados_depois_de_publicados_os_fatos_nao_voltam()
    {
        var venda = DoGalpao(_funil, _rotaSul);
        venda.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Ganho);

        venda.DescartarEventos();
        venda.Renomear("Galpão logístico de Jundiaí");

        Assert.Empty(venda.Eventos);
    }

    [Fact]
    public void A_venda_ganha_refeita_do_banco_nao_publica_o_ganho_de_novo()
    {
        var venda = DoGalpao(_funil, _rotaSul);
        venda.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Ganho);

        var refeita = Oportunidade.Reconstituir(venda.Id, venda.Estado);

        Assert.Equal(SituacaoDaOportunidade.Ganha, refeita.Situacao);
        Assert.Empty(refeita.Eventos);
    }
}
