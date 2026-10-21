using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using static CrmObras.Dominio.Testes.Oportunidades.VendasDeTeste;

namespace CrmObras.Dominio.Testes.Atividades;

public sealed class AgendaTests
{
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    private static readonly DateTimeOffset QuartaAs14h = new(2026, 10, 21, 14, 0, 0, Brasilia);

    private readonly Conta _rotaSul = RotaSul();

    private readonly Funil _funil = Funil.PadraoDaConstrutora();

    [Fact]
    public void Separa_as_atrasadas_das_de_hoje()
    {
        var segunda = Marcar("Segunda às 10h", Hora(19, 10));
        var hojeCedo = Marcar("Hoje às 9h", Hora(21, 9));
        var hojeTarde = Marcar("Hoje às 16h", Hora(21, 16));
        var hojeANoite = Marcar("Hoje às 23h30", Hora(21, 23, 30));
        var amanha = Marcar("Amanhã às 9h", Hora(22, 9));
        var feita = Marcar("Hoje às 15h, já feita", Hora(21, 15));
        feita.Concluir("Feita mais cedo", Hora(21, 11));
        var cancelada = Marcar("Ontem, cancelada", Hora(20, 9));
        cancelada.Cancelar("Não precisa", Hora(20, 8));
        var daAna = Marcar("Hoje às 10h, da Ana", Hora(21, 10), "ana.ribeiro");

        var agenda = Agenda.De("carlos.souza", [hojeANoite, amanha, feita, hojeTarde, daAna, hojeCedo, cancelada, segunda], [], QuartaAs14h);

        Assert.Equal(new DateOnly(2026, 10, 21), agenda.Dia);
        Assert.Equal([segunda, hojeCedo], agenda.Atrasadas);
        Assert.Equal([hojeTarde, hojeANoite], agenda.DeHoje);
    }

    [Fact]
    public void A_que_vence_agora_ainda_e_de_hoje()
    {
        var agora = Marcar("Às 14h", QuartaAs14h);

        var agenda = Agenda.De("carlos.souza", [agora], [], QuartaAs14h);

        Assert.Empty(agenda.Atrasadas);
        Assert.Equal([agora], agenda.DeHoje);
    }

    [Fact]
    public void O_dia_e_o_de_quem_olha_a_agenda()
    {
        var quatroHorasAFrente = TimeSpan.FromHours(1);
        var noiteEmBrasilia = Marcar("23h30 em Brasília", Hora(21, 23, 30));
        var tardeEmBrasilia = Marcar("19h30 em Brasília", Hora(21, 19, 30));

        var agenda = Agenda.De("carlos.souza", [noiteEmBrasilia, tardeEmBrasilia], [], QuartaAs14h.ToOffset(quatroHorasAFrente));

        // Quatro horas à frente, 23h30 de Brasília já é quinta, 3h30; e 19h30 é quarta, 23h30.
        Assert.Equal([tardeEmBrasilia], agenda.DeHoje);
    }

    [Fact]
    public void Mostra_as_vendas_abertas_sem_proximo_passo()
    {
        var comPasso = Venda("Com passo", Hora(15, 9));
        var proposta = Marcar("Mandar a proposta", Hora(22, 9), oportunidade: comPasso);
        var atrasada = Venda("Com passo atrasado", Hora(15, 9));
        var ligar = Marcar("Ligar de volta", Hora(19, 9), oportunidade: atrasada);
        var doEngenheiro = Venda("Com a visita do engenheiro", Hora(15, 9));
        var visita = Marcar("Visita técnica", Hora(23, 9), "joao.engenheiro", doEngenheiro);
        var soFeita = Venda("Só com atividade feita", Hora(16, 9));
        var feita = Marcar("Primeira reunião", Hora(16, 10), oportunidade: soFeita);
        feita.Concluir("Pediu orçamento", Hora(16, 11));
        var esquecida = Venda("Esquecida", Hora(10, 9));
        var ganha = Venda("Ganha", Hora(15, 9));
        ganha.Ganhar(_funil, Dinheiro.Reais(100_000m), Hora(17, 9));
        var daAna = Venda("Da Ana", Hora(15, 9), "ana.ribeiro");

        var agenda = Agenda.De(
            "carlos.souza",
            [proposta, ligar, visita, feita],
            [comPasso, atrasada, doEngenheiro, soFeita, esquecida, ganha, daAna],
            QuartaAs14h);

        Assert.Equal([esquecida, soFeita], agenda.SemProximoPasso);
    }

    private static DateTimeOffset Hora(int dia, int hora, int minuto = 0) => new(2026, 10, dia, hora, minuto, 0, Brasilia);

    private Atividade Marcar(string assunto, DateTimeOffset prevista, string responsavel = "carlos.souza", Oportunidade? oportunidade = null) =>
        new(_rotaSul, new DadosDaAtividade(TipoDeAtividade.Ligacao, assunto, prevista), responsavel, Hora(1, 8), oportunidade);

    private Oportunidade Venda(string titulo, DateTimeOffset em, string responsavel = "carlos.souza") =>
        new(_rotaSul.Id, titulo, _funil, responsavel, em);
}
