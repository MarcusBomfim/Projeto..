using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class AtividadeReconstituidaTests
{
    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    private readonly Conta _rotaSul = RotaSul();

    public static TheoryData<string> Situacoes => ["pendente e adiada", "concluída", "cancelada"];

    [Theory]
    [MemberData(nameof(Situacoes))]
    public void Volta_como_estava(string situacao)
    {
        var atividade = NaSituacao(situacao);

        var refeita = Atividade.Reconstituir(atividade.Id, atividade.Estado);

        Assert.Equal(atividade.Id, refeita.Id);
        Assert.Equal(atividade.Estado, refeita.Estado);
    }

    [Fact]
    public void Volta_mesmo_com_a_venda_fechada_e_o_contato_fora_da_conta()
    {
        var venda = DoGalpao(_funil, _rotaSul);
        var marina = _rotaSul.Contatos[0];
        var visita = new Atividade(_rotaSul, new DadosDaAtividade(TipoDeAtividade.Visita, "Visita ao terreno", Segunda.AddDays(2), marina.Id), "carlos.souza", Segunda, venda);
        venda.Perder(_funil, new Perda(MotivoDaPerda.Prazo), Segunda.AddDays(1));
        _rotaSul.DefinirPrincipal(_rotaSul.Contatos[1].Id);
        _rotaSul.RemoverContato(marina.Id);

        var refeita = Atividade.Reconstituir(visita.Id, visita.Estado);

        Assert.Equal((venda.Id, (ContatoId?)marina.Id), (refeita.Oportunidade, refeita.Com));
    }

    private static readonly Dictionary<string, Func<EstadoDaAtividade, EstadoDaAtividade>> Estragos = new()
    {
        ["pendente, com resultado"] = estado => estado with { Situacao = SituacaoDaAtividade.Pendente },
        ["pendente, encerrada"] = estado => estado with { Situacao = SituacaoDaAtividade.Pendente, Resultado = null },
        ["concluída, sem resultado"] = estado => estado with { Resultado = null },
        ["concluída, com resultado em branco"] = estado => estado with { Resultado = " " },
        ["concluída, com espaço no resultado"] = estado => estado with { Resultado = " Feita" },
        ["concluída, com resultado longo demais"] = estado => estado with { Resultado = new string('r', 1001) },
        ["concluída, sem data"] = estado => estado with { EncerradaEm = null },
        ["concluída antes de marcada"] = estado => estado with { EncerradaEm = estado.CriadaEm.AddMinutes(-1) },
        ["concluída, com motivo de cancelamento"] = estado => estado with { MotivoDoCancelamento = "Chuva" },
        ["cancelada, sem motivo"] = estado => estado with { Situacao = SituacaoDaAtividade.Cancelada, Resultado = null },
        ["cancelada, com resultado"] = estado => estado with { Situacao = SituacaoDaAtividade.Cancelada, MotivoDoCancelamento = "Chuva" },
        ["adiada um número negativo de vezes"] = estado => estado with { Adiamentos = -1 },
    };

    public static TheoryData<string> Estragados => [.. Estragos.Keys];

    [Theory]
    [MemberData(nameof(Estragados))]
    public void O_estado_que_as_regras_nao_produzem_e_recusado(string caso)
    {
        var concluida = NaSituacao("concluída");
        var estragado = Estragos[caso](concluida.Estado);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Atividade.Reconstituir(concluida.Id, estragado));

        Assert.Equal($"A atividade Retorno do orçamento está gravada como {estragado.Situacao}, num estado que as regras não produzem.", erro.Message);
    }

    [Fact]
    public void Os_textos_passam_pelas_regras_de_sempre()
    {
        var pendente = NaSituacao("pendente e adiada");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Atividade.Reconstituir(
            pendente.Id,
            pendente.Estado with { Dados = pendente.Estado.Dados with { Assunto = "" }, Responsavel = new string('r', 101) }));

        Assert.Equal("Falta o assunto da atividade. O responsável pela atividade passa de 100 caracteres.", erro.Message);
    }

    private Atividade NaSituacao(string situacao)
    {
        var atividade = new Atividade(
            _rotaSul,
            new DadosDaAtividade(TipoDeAtividade.Ligacao, "Retorno do orçamento", Segunda.AddDays(2), _rotaSul.Contatos[1].Id, "Levar a planilha"),
            "carlos.souza",
            Segunda,
            DoGalpao(_funil, _rotaSul));

        switch (situacao)
        {
            case "pendente e adiada":
                atividade.Reagendar(Segunda.AddDays(4), Segunda.AddDays(1));
                atividade.Reagendar(Segunda.AddDays(6), Segunda.AddDays(3));
                break;
            case "concluída":
                atividade.Concluir("Pediu desconto", Segunda.AddDays(2));
                break;
            case "cancelada":
                atividade.Cancelar("Cliente viajou", Segunda.AddDays(1));
                break;
        }

        return atividade;
    }
}
