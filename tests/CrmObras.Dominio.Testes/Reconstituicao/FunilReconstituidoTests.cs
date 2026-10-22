using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class FunilReconstituidoTests
{
    [Fact]
    public void Volta_com_as_etapas_em_uso_as_arquivadas_e_as_finais_como_estavam()
    {
        var funil = Funil.PadraoDaConstrutora();
        funil.AdicionarEtapa(new DadosDaEtapa("Aprovação de crédito", 70, 20, Requisitos.QuemDecide));
        funil.ArquivarEtapa(funil.Abertas[1].Id);
        funil.RenomearEtapa(funil.Ganha.Id, "Contrato assinado");
        funil.RenomearEtapa(funil.Perdida.Id, "Não fechou");

        var refeito = Refazer(funil);

        Assert.Equal((funil.Id, funil.Nome), (refeito.Id, refeito.Nome));
        Assert.Equal(Descrever(funil.Abertas), Descrever(refeito.Abertas));
        Assert.Equal(Descrever(funil.Arquivadas), Descrever(refeito.Arquivadas));
        Assert.Equal(Descrever([funil.Ganha, funil.Perdida]), Descrever([refeito.Ganha, refeito.Perdida]));
        Assert.Equal(funil.Abertas.Select(etapa => funil.RequisitosPara(etapa.Id)), refeito.Abertas.Select(etapa => refeito.RequisitosPara(etapa.Id)));
    }

    [Fact]
    public void Com_a_final_renomeada_o_nome_dela_pode_ser_de_uma_etapa_aberta()
    {
        var funil = Funil.PadraoDaConstrutora();
        funil.RenomearEtapa(funil.Ganha.Id, "Contrato assinado");
        funil.AdicionarEtapa(new DadosDaEtapa("Ganha", 90));

        var refeito = Refazer(funil);

        Assert.Equal("Ganha", refeito.Abertas[^1].Nome);
        Assert.Equal("Contrato assinado", refeito.Ganha.Nome);
    }

    [Fact]
    public void As_abertas_passam_pelas_regras_de_quem_cria_o_funil()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Funil.Reconstituir(
            FunilId.Novo(),
            "Vendas",
            [(EtapaId.Novo(), new DadosDaEtapa("Proposta", 60)), (EtapaId.Novo(), new DadosDaEtapa("Visita", 20, Requisitos: Requisitos.QuemDecide))],
            [],
            (EtapaId.Novo(), "Ganha"),
            (EtapaId.Novo(), "Perdida")));

        Assert.Equal("Visita (20%) não pode vir depois de Proposta (60%): a probabilidade não cai conforme a venda avança.", erro.Message);
    }

    [Fact]
    public void A_arquivada_passa_pelas_regras_da_etapa_e_nao_repete_nome()
    {
        var invalida = Assert.Throws<ExcecaoDeDominio>(() => Refazer(arquivadas: [(EtapaId.Novo(), new DadosDaEtapa("Visita", 0))]));
        var repetida = Assert.Throws<ExcecaoDeDominio>(() => Refazer(arquivadas: [(EtapaId.Novo(), new DadosDaEtapa("contato", 20))]));

        Assert.Equal("A probabilidade da etapa Visita vai de 1% a 99%: 0% é perder, e 100%, ganhar.", invalida.Message);
        Assert.Equal("Já existe a etapa Contato.", repetida.Message);
    }

    [Fact]
    public void A_mesma_etapa_nao_volta_duas_vezes()
    {
        var contato = EtapaId.Novo();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(contato, arquivadas: [(contato, new DadosDaEtapa("Visita", 20))]));

        Assert.Equal("O funil Vendas está gravado com a mesma etapa duas vezes.", erro.Message);
    }

    [Fact]
    public void As_finais_tem_nome_e_nomes_diferentes()
    {
        var semNome = Assert.Throws<ExcecaoDeDominio>(() => Refazer(ganha: " ", perdida: ""));
        var iguais = Assert.Throws<ExcecaoDeDominio>(() => Refazer(ganha: "Fechou", perdida: "fechou"));
        var comoAberta = Assert.Throws<ExcecaoDeDominio>(() => Refazer(ganha: "contato"));

        Assert.Equal("Falta o nome da etapa final de ganho. Falta o nome da etapa final de perda.", semNome.Message);
        Assert.Equal("As duas etapas finais se chamam Fechou.", iguais.Message);
        Assert.Equal("Contato é etapa final, e o funil já a tem: a lista é só das etapas abertas.", comoAberta.Message);
    }

    private static Funil Refazer(Funil funil) =>
        Funil.Reconstituir(
            funil.Id,
            funil.Nome,
            [.. funil.Abertas.Select(Gravada)],
            [.. funil.Arquivadas.Select(Gravada)],
            (funil.Ganha.Id, funil.Ganha.Nome),
            (funil.Perdida.Id, funil.Perdida.Nome));

    private static Funil Refazer(
        EtapaId? contato = null,
        IReadOnlyList<(EtapaId Id, DadosDaEtapa Dados)>? arquivadas = null,
        string ganha = "Ganha",
        string perdida = "Perdida") =>
        Funil.Reconstituir(
            FunilId.Novo(),
            "Vendas",
            [(contato ?? EtapaId.Novo(), new DadosDaEtapa("Contato", 10))],
            arquivadas ?? [],
            (EtapaId.Novo(), ganha),
            (EtapaId.Novo(), perdida));

    private static (EtapaId Id, DadosDaEtapa Dados) Gravada(Etapa etapa) =>
        (etapa.Id, new DadosDaEtapa(etapa.Nome, etapa.Probabilidade, etapa.DiasEsperados, etapa.Requisitos));

    private static string[] Descrever(IEnumerable<Etapa> etapas) =>
        [.. etapas.Select(etapa => $"{etapa.Id} {etapa.Tipo} {etapa.Nome} {etapa.Probabilidade} {etapa.DiasEsperados} {etapa.Requisitos} {etapa.Arquivada}")];
}
