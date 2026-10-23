using System.Net;
using CrmObras.Api.Recursos.Atividades;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Dominio.Atividades;

namespace CrmObras.Api.Testes;

/*
 * As atividades pela API — marcar, adiar, passar adiante, concluir com o
 * resultado, cancelar com o motivo — e a agenda de cada vendedor, no dia do
 * fuso de quem olha (ADR-0006).
 */
public sealed class AtividadesTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly CenarioDeVenda _cenario = new(api);

    private static DateTimeOffset Em(int dia, int mes, int hora, int minuto = 0) => CenarioDeVenda.Em(dia, mes, hora, minuto);

    private Task<HttpResponseMessage> Marcar(Guid conta, string assunto, DateTimeOffset quando, Guid? com = null, Guid? oportunidade = null) =>
        _cenario.Carlos.Criar("/api/atividades", new { conta, tipo = "Ligacao", assunto, previstaPara = quando, com, oportunidade });

    [Fact]
    public async Task Marca_em_nome_de_quem_pede_com_o_contato_e_a_venda()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, sergio) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;

        var criada = await Marcar(filial, "Retorno da proposta", Em(27, 11, 10), sergio, venda).ComSucesso();
        var atividade = await criada.Ler<AtividadeResposta>();

        Assert.Equal((HttpStatusCode.Created, "\"1\""), (criada.StatusCode, criada.Headers.ETag?.Tag));
        Assert.Equal(
            ("carlos.souza", TipoDeAtividade.Ligacao, sergio, venda, SituacaoDaAtividade.Pendente, false),
            (atividade.Responsavel, atividade.Tipo, atividade.Com, atividade.Oportunidade, atividade.Situacao, atividade.Atrasada));
        Assert.Equal((Em(27, 11, 10), Em(26, 11, 9)), (atividade.PrevistaPara, atividade.CriadaEm));
    }

    [Fact]
    public async Task Contato_de_fora_da_conta_e_venda_fechada_sao_422_de_uma_vez()
    {
        var (filial, _, _) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/ganho", new { valorFechado = 100_000m }, versao: 1).ComSucesso();

        var resposta = await Marcar(filial, "Pós-venda", Em(27, 11, 10), Guid.CreateVersion7(), venda);
        var detalhe = (await resposta.Problema()).Detail;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("não tem o contato", detalhe, StringComparison.Ordinal);
        Assert.Contains("está fechada: a atividade nova é da conta, e não da venda", detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adiar_conta_antecipar_nao_e_para_antes_de_agora_e_422()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var atividade = await Marcar(filial, "Retorno", Em(27, 11, 10)).IdCriado();

        var adiada = await _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/atividades/{atividade}/data", new { para = Em(30, 11, 10) }, versao: 1)
            .LerComSucesso<AtividadeResposta>();
        var antecipada = await _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/atividades/{atividade}/data", new { para = Em(28, 11, 10) }, versao: 2)
            .LerComSucesso<AtividadeResposta>();
        var noPassado = await _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/atividades/{atividade}/data", new { para = Em(25, 11, 10) }, versao: 3);

        Assert.Equal((Em(30, 11, 10), 1), (adiada.PrevistaPara, adiada.Adiamentos));
        Assert.Equal((Em(28, 11, 10), 1), (antecipada.PrevistaPara, antecipada.Adiamentos));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noPassado.StatusCode);
    }

    [Fact]
    public async Task Concluir_pede_o_resultado_e_a_encerrada_nao_muda_mais()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var atividade = await Marcar(filial, "Retorno da proposta", Em(27, 11, 10)).IdCriado();
        _cenario.Relogio(Em(27, 11, 14, 30));

        var atrasada = await _cenario.Leitor.LerComSucesso<AtividadeResposta>($"/api/atividades/{atividade}");
        var semResultado = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/atividades/{atividade}/conclusao", new { resultado = "  " }, versao: 1);
        var concluida = await _cenario.Carlos
            .Enviar(HttpMethod.Post, $"/api/atividades/{atividade}/conclusao", new { resultado = "Achou caro: pede 12% de desconto" }, versao: 1)
            .LerComSucesso<AtividadeResposta>();
        var cancelada = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/atividades/{atividade}/cancelamento", new { motivo = "Engano" }, versao: 2);

        Assert.True(atrasada.Atrasada);
        Assert.Equal("Concluir a atividade Retorno da proposta pede o resultado: o que foi dito, o que ficou combinado.", (await semResultado.Problema()).Detail);
        Assert.Equal(
            (SituacaoDaAtividade.Concluida, "Achou caro: pede 12% de desconto", Em(27, 11, 14, 30), false),
            (concluida.Situacao, concluida.Resultado, concluida.EncerradaEm!.Value, concluida.Atrasada));
        Assert.Equal("A atividade Retorno da proposta já foi concluída, e não muda mais.", (await cancelada.Problema()).Detail);
    }

    [Fact]
    public async Task A_agenda_comeca_pelas_atrasadas_e_mostra_a_venda_sem_proximo_passo()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, sergio) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();
        var galpao = (await _cenario.Venda(filial, funil, "Galpão")).Id;
        var portaria = (await _cenario.Venda(filial, funil, "Portaria")).Id;
        var retorno = await Marcar(filial, "Retorno da proposta", Em(27, 11, 10), sergio, galpao).IdCriado();
        var visita = await Marcar(filial, "Visita à obra", Em(27, 11, 16)).IdCriado();
        await Marcar(filial, "Reunião de segunda", Em(30, 11, 9)).ComSucesso();
        _cenario.Relogio(Em(27, 11, 14));

        var agenda = await _cenario.Carlos.LerComSucesso<AgendaResposta>("/api/agenda");

        Assert.Equal((new DateOnly(2026, 11, 27), "carlos.souza"), (agenda.Dia, agenda.Responsavel));
        Assert.Equal([(retorno, "Rota Sul Logística — Campinas")], agenda.Atrasadas.Select(atividade => (atividade.Id, atividade.NomeDaConta)));
        Assert.Equal([visita], agenda.DeHoje.Select(atividade => atividade.Id));
        Assert.Equal([(portaria, 1)], agenda.SemProximoPasso.Select(venda => (venda.Id, venda.Versao)));
    }

    [Fact]
    public async Task O_dia_da_agenda_e_o_do_fuso_de_quem_olha()
    {
        // A reunião das 22h em Brasília é de hoje para quem está aqui — e de
        // amanhã, à 1h da manhã, para quem olha a agenda de Lisboa.
        _cenario.Relogio(Em(27, 11, 14));
        var (filial, _, _) = await _cenario.Filial();
        var reuniao = await Marcar(filial, "Reunião com Lisboa", Em(27, 11, 22)).IdCriado();

        var deBrasilia = await _cenario.Carlos.LerComSucesso<AgendaResposta>("/api/agenda");
        var deLisboa = await _cenario.Carlos.LerComSucesso<AgendaResposta>("/api/agenda?fuso=Europe/Lisbon");
        var fusoInventado = await _cenario.Carlos.GetAsync("/api/agenda?fuso=America/Atlantida", Cancelamento);

        Assert.Equal(new DateOnly(2026, 11, 27), deBrasilia.Dia);
        Assert.Equal([reuniao], deBrasilia.DeHoje.Select(atividade => atividade.Id));
        Assert.Equal(new DateOnly(2026, 11, 27), deLisboa.Dia);
        Assert.Empty(deLisboa.DeHoje);
        Assert.Equal(HttpStatusCode.BadRequest, fusoInventado.StatusCode);
    }

    [Fact]
    public async Task A_atividade_passada_adiante_vai_para_a_agenda_de_quem_a_recebe()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var visita = await Marcar(filial, "Visita técnica", Em(26, 11, 15)).IdCriado();

        await _cenario.Carlos.Enviar(HttpMethod.Put, $"/api/atividades/{visita}/responsavel", new NovoResponsavel("joao.engenharia"), versao: 1).ComSucesso();
        var doJoao = await _cenario.Leitor.LerComSucesso<AgendaResposta>("/api/agenda?responsavel=joao.engenharia");
        var doCarlos = await _cenario.Carlos.LerComSucesso<AgendaResposta>("/api/agenda");

        Assert.Equal([visita], doJoao.DeHoje.Select(atividade => atividade.Id));
        Assert.Empty(doCarlos.DeHoje);
    }

    [Fact]
    public async Task Lista_as_atividades_da_conta_pela_data()
    {
        _cenario.Relogio(Em(26, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var depois = await Marcar(filial, "Reunião", Em(30, 11, 9)).IdCriado();
        var antes = await Marcar(filial, "Ligação", Em(27, 11, 10)).IdCriado();

        var atividades = await _cenario.Leitor.LerComSucesso<List<ResumoDaAtividadeResposta>>($"/api/atividades?conta={filial}");

        Assert.Equal([antes, depois], atividades.Select(atividade => atividade.Id));
    }
}
