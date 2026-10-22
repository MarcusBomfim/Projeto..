using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class OportunidadeReconstituidaTests
{
    private static readonly PassagemPorEtapa[] SemPassagens = [];

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    public static TheoryData<string> Situacoes => ["aberta", "ganha", "perdida", "reaberta"];

    [Theory]
    [MemberData(nameof(Situacoes))]
    public void Volta_como_estava(string situacao)
    {
        var venda = NaSituacao(situacao);

        var refeita = Oportunidade.Reconstituir(venda.Id, venda.Estado);

        Assert.Equal(venda.Id, refeita.Id);
        Assert.Equal(venda.Estado with { Passagens = SemPassagens }, refeita.Estado with { Passagens = SemPassagens });
        Assert.Equal(venda.Passagens, refeita.Passagens);
    }

    [Fact]
    public void Volta_numa_etapa_que_hoje_pede_o_que_a_venda_nao_tem()
    {
        // A venda entrou no orçamento quando ele não pedia nada; hoje pede quem decide.
        var funil = new Funil("Vendas", [new("Contato", 10), new("Orçamento", 40)]);
        var venda = new Oportunidade(_rotaSul.Id, "Galpão", funil, "carlos.souza", Segunda);
        venda.MoverPara(funil, funil.Abertas[1].Id, Segunda.AddDays(1));
        funil.AlterarEtapa(funil.Abertas[1].Id, new DadosDaEtapa("Orçamento", 40, Requisitos: Requisitos.QuemDecide));

        var refeita = Oportunidade.Reconstituir(venda.Id, venda.Estado);

        Assert.Equal(funil.Abertas[1].Id, refeita.Etapa);
    }

    private static readonly Dictionary<string, (Func<EstadoDaOportunidade, EstadoDaOportunidade> Estragar, string Problema)> Estragos = new()
    {
        ["sem passagem"] = (estado => estado with { Passagens = SemPassagens }, "não passou por etapa nenhuma"),
        ["começando depois da abertura"] = (estado => estado with { Passagens = [estado.Passagens[0] with { EntrouEm = estado.CriadaEm.AddHours(1) }, .. estado.Passagens.Skip(1)] }, "a primeira passagem não começa quando a venda foi aberta"),
        ["com um buraco entre as passagens"] = (estado => estado with { Passagens = [estado.Passagens[0] with { SaiuEm = estado.Passagens[0].SaiuEm!.Value.AddHours(-1) }, .. estado.Passagens.Skip(1)] }, "a passagem 1 não termina quando a seguinte começa"),
        ["com saída na atual"] = (estado => estado with { Passagens = [.. estado.Passagens.SkipLast(1), estado.Passagens[^1] with { SaiuEm = estado.Passagens[^1].EntrouEm.AddDays(1) }] }, "a passagem atual tem saída"),
        ["saindo antes de entrar"] = (estado => estado with { Passagens = [estado.Passagens[0] with { SaiuEm = estado.CriadaEm.AddDays(-1) }, estado.Passagens[1] with { EntrouEm = estado.CriadaEm.AddDays(-1) }, .. estado.Passagens.Skip(2)] }, "uma passagem termina antes de começar"),
        ["com valor estimado negativo"] = (estado => estado with { ValorEstimado = Dinheiro.Reais(-1m) }, "o valor estimado não é positivo, em centavos"),
        ["com valor estimado fora do centavo"] = (estado => estado with { ValorEstimado = Dinheiro.Reais(1.001m) }, "o valor estimado não é positivo, em centavos"),
        ["aberta, com fechamento"] = (estado => estado with { Situacao = SituacaoDaOportunidade.Aberta, FechadaEm = null, ValorFechado = Dinheiro.Reais(1m) }, "está aberta, e tem fechamento"),
        ["ganha, sem valor fechado"] = (estado => estado with { ValorFechado = null }, "está ganha, e não tem o valor fechado, ou tem perda"),
        ["ganha, com valor fechado zero"] = (estado => estado with { ValorFechado = Dinheiro.Zero }, "o valor fechado não é positivo, em centavos"),
        ["ganha, com perda"] = (estado => estado with { Perda = new Perda(MotivoDaPerda.Preco) }, "está ganha, e não tem o valor fechado, ou tem perda"),
        ["fechada fora da última entrada"] = (estado => estado with { FechadaEm = estado.FechadaEm!.Value.AddHours(1) }, "o fechamento não é a entrada na última etapa"),
        ["perdida, sem o motivo"] = (estado => estado with { Situacao = SituacaoDaOportunidade.Perdida, ValorFechado = null }, "está perdida, e não tem o motivo, ou tem valor fechado"),
        ["perdida, com valor fechado"] = (estado => estado with { Situacao = SituacaoDaOportunidade.Perdida, Perda = new Perda(MotivoDaPerda.Preco) }, "está perdida, e não tem o motivo, ou tem valor fechado"),
        ["perdida para ninguém"] = (estado => estado with { Situacao = SituacaoDaOportunidade.Perdida, ValorFechado = null, Perda = new Perda(MotivoDaPerda.ConcorrenteEscolhido) }, "a perda não diz o que o motivo pede"),
        ["perdida com espaços"] = (estado => estado with { Situacao = SituacaoDaOportunidade.Perdida, ValorFechado = null, Perda = new Perda(MotivoDaPerda.Preco, " Beta ") }, "a perda não diz o que o motivo pede"),
    };

    public static TheoryData<string> Estragados => [.. Estragos.Keys];

    [Theory]
    [MemberData(nameof(Estragados))]
    public void O_estado_que_as_regras_nao_produzem_e_recusado(string caso)
    {
        var (estragar, problema) = Estragos[caso];
        var ganha = NaSituacao("ganha");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Oportunidade.Reconstituir(ganha.Id, estragar(ganha.Estado)));

        Assert.Equal($"A oportunidade Galpão de 1.200 m² em Jundiaí está gravada num estado que as regras não produzem: {problema}.", erro.Message);
    }

    [Fact]
    public void Diz_todos_os_problemas_de_uma_vez()
    {
        var ganha = NaSituacao("ganha");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Oportunidade.Reconstituir(ganha.Id, ganha.Estado with { ValorEstimado = Dinheiro.Zero, ValorFechado = null }));

        Assert.EndsWith(
            "o valor estimado não é positivo, em centavos; está ganha, e não tem o valor fechado, ou tem perda.",
            erro.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void O_titulo_e_o_responsavel_passam_pelas_regras_de_sempre()
    {
        var aberta = NaSituacao("aberta");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Oportunidade.Reconstituir(aberta.Id, aberta.Estado with { Titulo = "", Responsavel = " " }));

        Assert.Equal("Falta o título da oportunidade. Falta o responsável pela oportunidade.", erro.Message);
    }

    private Oportunidade NaSituacao(string situacao)
    {
        var venda = new Oportunidade(_rotaSul.Id, "Galpão de 1.200 m² em Jundiaí", _funil, "carlos.souza", Segunda, LeadId.Novo());
        venda.Estimar(Dinheiro.Reais(2_400_000m));
        venda.PreverFechamento(Dezembro);
        venda.DefinirQuemDecide(_rotaSul, _rotaSul.Contatos[1].Id);
        venda.MoverPara(_funil, _funil.Abertas[3].Id, Segunda.AddDays(5));

        switch (situacao)
        {
            case "ganha":
                venda.Ganhar(_funil, Dinheiro.Reais(2_350_000m), Segunda.AddDays(30));
                break;
            case "perdida":
                venda.Perder(_funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, "Construtora Beta", "Prazo menor"), Segunda.AddDays(30));
                break;
            case "reaberta":
                venda.Perder(_funil, new Perda(MotivoDaPerda.Prazo), Segunda.AddDays(30));
                venda.Reabrir(_funil, _funil.Abertas[2].Id, Segunda.AddDays(40));
                break;
        }

        return venda;
    }
}
