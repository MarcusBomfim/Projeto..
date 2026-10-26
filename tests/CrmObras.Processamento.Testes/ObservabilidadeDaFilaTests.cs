using System.Diagnostics;
using System.Net;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Hospedagem;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrmObras.Processamento.Testes;

/*
 * A fila vista de fora (ADR-0018): o tratamento de cada mensagem continua o
 * rastro de quem publicou o fato — e o leva ao planejamento-obras —, a volta
 * vazia não deixa rastro, cada tentativa é medida, e a fila parada deixa de
 * estar viva.
 */
public sealed class ObservabilidadeDaFilaTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private const string VendaGanha = "venda-ganha";

    [Fact]
    public async Task O_tratamento_continua_o_rastro_de_quem_publicou_o_fato_ate_o_planejamento()
    {
        using var rastros = new RastrosDoTeste();
        ActivityContext pedido;

        // O pedido que registra o fato: na API, o pedido HTTP.
        using (var atividade = RastrosDoTeste.Fonte.StartActivity("pedido de teste"))
        {
            pedido = atividade!.Context;
            await Vendas().GanharUma();
        }

        await using var processamento = Processamento();
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var doPedido = rastros.Do(pedido.TraceId);
        var publicacao = Assert.Single(doPedido, atividade => atividade.DisplayName == $"publicar {VendaGanha}");
        var tratamento = Assert.Single(doPedido, atividade => atividade.DisplayName == $"tratar {VendaGanha}");
        var aoPlanejamento = Assert.Single(doPedido, atividade => atividade.Source.Name == "System.Net.Http");

        Assert.Equal((ActivityKind.Producer, pedido.SpanId), (publicacao.Kind, publicacao.ParentSpanId));
        Assert.Equal((ActivityKind.Consumer, publicacao.SpanId), (tratamento.Kind, tratamento.ParentSpanId));
        Assert.Equal(
            ("tratada", Construtora.ToString(), 1),
            (tratamento.GetTagItem("crm.mensagem.resultado"), tratamento.GetTagItem("crm.construtora.id"), tratamento.GetTagItem("crm.mensagem.tentativa")));
        Assert.Equal(
            Assert.Single(Planejamento.Abertos).Value.ToString(),
            tratamento.GetTagItem("crm.planejamento.cronograma.id"));

        // O pedido ao planejamento está dentro do tratamento, e leva o rastro
        // adiante: a API do planejamento continua o mesmo.
        Assert.Equal((ActivityKind.Client, tratamento.SpanId), (aoPlanejamento.Kind, aoPlanejamento.ParentSpanId));
        Assert.StartsWith($"00-{pedido.TraceId}-{aoPlanejamento.SpanId}-", Assert.Single(Planejamento.Recebidos).Traceparent, StringComparison.Ordinal);

        // O que o tratamento fez no banco também está dentro dele.
        Assert.Contains(doPedido, atividade => atividade.Source.Name == "Npgsql" && atividade.ParentSpanId == tratamento.SpanId);
    }

    [Fact]
    public async Task Mensagem_publicada_sem_rastro_comeca_um_rastro_novo()
    {
        // Gravada sem ninguém rastreando — como uma que chegou por um caminho sem instrumentação.
        await Vendas().GanharUma();

        using var rastros = new RastrosDoTeste();
        await using var processamento = Processamento();
        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var tratamento = Assert.Single(rastros.Todos, atividade => atividade.DisplayName == $"tratar {VendaGanha}");
        Assert.Equal(default, tratamento.ParentSpanId);
    }

    [Fact]
    public async Task Olhar_a_fila_vazia_nao_deixa_rastro()
    {
        using var rastros = new RastrosDoTeste();
        await using var processamento = Processamento();

        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.Empty(rastros.Todos);
    }

    [Fact]
    public async Task Abrir_a_escuta_de_mensagens_novas_nao_deixa_rastro()
    {
        using var rastros = new RastrosDoTeste();
        using var registro = new RegistroEmMemoria();
        await using var espera = new EsperaPorMensagens(Banco.ConexaoDoProcessador, registro.Para<EsperaPorMensagens>());

        await espera.Preparar(Cancelamento);
        Assert.False(await espera.Aguardar(TimeSpan.FromMilliseconds(10), Cancelamento));

        // Nem a conexão aberta, nem o listen: é olhar a fila.
        Assert.Empty(rastros.Todos);
        Assert.Equal(0, registro.Quantas("A escuta"));
    }

    [Fact]
    public async Task A_falha_do_tratamento_fica_no_rastro_com_a_excecao()
    {
        await Vendas().GanharUma();
        Planejamento.FalharUmaVez(HttpStatusCode.ServiceUnavailable, """{"title":"Indisponível.","status":503}""");

        using var rastros = new RastrosDoTeste();
        await using var processamento = Processamento();
        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var tratamento = Assert.Single(rastros.Todos, atividade => atividade.DisplayName == $"tratar {VendaGanha}");
        Assert.Equal((ActivityStatusCode.Error, "falhou"), (tratamento.Status, tratamento.GetTagItem("crm.mensagem.resultado")));

        var excecao = Assert.Single(tratamento.Events, evento => evento.Name == "exception");
        Assert.Equal(
            "O planejamento-obras respondeu 503 ao abrir o cronograma: Indisponível.",
            excecao.Tags.Single(atributo => atributo.Key == "exception.message").Value);
    }

    [Fact]
    public async Task Cada_tentativa_e_medida_pelo_tipo_e_pelo_resultado_com_a_espera_desde_o_fato()
    {
        await Vendas().GanharUma();
        Planejamento.FalharUmaVez(HttpStatusCode.ServiceUnavailable);
        await using var processamento = Processamento();
        using var mensagens = processamento.Metrica<long>(MetricasDaCaixaDeSaida.Mensagens);
        using var esperas = processamento.Metrica<double>(MetricasDaCaixaDeSaida.Espera);
        using var duracoes = processamento.Metrica<double>(MetricasDaCaixaDeSaida.Duracao);

        // A primeira tentativa, dois segundos depois do fato, falha; a segunda,
        // depois da espera de trinta segundos, trata.
        Relogio.Adiantar(TimeSpan.FromSeconds(2));
        await processamento.Processador.ProcessarPendentes(Cancelamento);
        Relogio.Adiantar(TimeSpan.FromSeconds(30));
        await processamento.Processador.ProcessarPendentes(Cancelamento);

        Assert.Equal(
            [(VendaGanha, "falhou", 1L), (VendaGanha, "tratada", 1L)],
            mensagens.GetMeasurementSnapshot().Select(medida => (Tipo(medida.Tags), Resultado(medida.Tags), medida.Value)));
        Assert.Equal([2d, 32d], esperas.GetMeasurementSnapshot().Select(medida => medida.Value));

        // A duração é de verdade, e não do relógio do teste, que fica parado
        // enquanto a mensagem é tratada.
        Assert.Equal(2, duracoes.GetMeasurementSnapshot().Count);
        Assert.All(duracoes.GetMeasurementSnapshot(), medida => Assert.InRange(medida.Value, double.Epsilon, 30));
    }

    [Fact]
    public async Task Na_decima_falha_a_metrica_diz_que_a_fila_desistiu()
    {
        await Vendas().GanharUma();
        await using var processamento = Processamento(servicos => servicos.AddSingleton(new Falhas(vezes: int.MaxValue)).TratarTambemCom<TratadorQueFalha>());
        using var mensagens = processamento.Metrica<long>(MetricasDaCaixaDeSaida.Mensagens);

        for (var tentativa = 1; tentativa <= FilaDaCaixaDeSaida.TentativasAntesDeDesistir; tentativa++)
        {
            await processamento.Processador.ProcessarPendentes(Cancelamento);
            Relogio.Adiantar(TimeSpan.FromHours(1));
        }

        Assert.Equal(
            [.. Enumerable.Repeat("falhou", FilaDaCaixaDeSaida.TentativasAntesDeDesistir - 1), "abandonada"],
            mensagens.GetMeasurementSnapshot().Select(medida => Resultado(medida.Tags)));
    }

    [Fact]
    public async Task Cada_execucao_de_rotina_vira_um_rastro_com_os_comandos_dela()
    {
        using var rastros = new RastrosDoTeste();
        await using var processamento = Processamento();
        var rotinas = processamento.Montar<ServicoDasRotinas>();

        await rotinas.StartAsync(Cancelamento);
        await Ate(() => Task.FromResult(Execucoes(rastros).Count == 2), TimeSpan.FromSeconds(15), "as duas rotinas rodarem");
        await rotinas.StopAsync(Cancelamento);

        Assert.Equal(
            ["rotina LimpezaDaCaixaDeSaida", "rotina LimpezaDasChavesDeIdempotencia"],
            Execucoes(rastros).Select(execucao => execucao.DisplayName).Order(StringComparer.Ordinal));
        Assert.All(
            Execucoes(rastros),
            execucao => Assert.Contains(rastros.Todos, atividade => atividade.Source.Name == "Npgsql" && atividade.ParentSpanId == execucao.SpanId));
    }

    private static List<Activity> Execucoes(RastrosDoTeste rastros) =>
        [.. rastros.Todos.Where(atividade => atividade.DisplayName.StartsWith("rotina ", StringComparison.Ordinal))];

    [Fact]
    public async Task A_fila_que_para_de_girar_deixa_de_estar_viva()
    {
        await using var processamento = Processamento();
        var fila = processamento.Montar<FilaGirando>();
        var contexto = new HealthCheckContext();

        // Quatro intervalos de trinta segundos sem sinal ainda é vida.
        Relogio.Adiantar(TimeSpan.FromMinutes(2));
        Assert.Equal(HealthStatus.Healthy, (await fila.CheckHealthAsync(contexto, Cancelamento)).Status);

        Relogio.Adiantar(TimeSpan.FromSeconds(1));
        var parada = await fila.CheckHealthAsync(contexto, Cancelamento);
        Assert.Equal((HealthStatus.Unhealthy, "A fila não dá sinal de vida há 121 segundos."), (parada.Status, parada.Description));

        // Uma volta, mesmo sem mensagem nenhuma, e ela está viva de novo.
        await processamento.Processador.ProcessarPendentes(Cancelamento);
        Assert.Equal(HealthStatus.Healthy, (await fila.CheckHealthAsync(contexto, Cancelamento)).Status);
    }

    [Fact]
    public async Task Com_o_banco_fora_do_ar_a_fila_continua_viva()
    {
        await using var processamento = new AmbienteDoProcessador(Banco, Relogio, Planejamento.Endereco, conexao: EsperaPorMensagensTests.BancoForaDoAr);
        var fila = processamento.Montar<FilaGirando>();

        Relogio.Adiantar(TimeSpan.FromMinutes(10));
        await Assert.ThrowsAnyAsync<Exception>(() => processamento.Processador.ProcessarPendentes(Cancelamento));

        // Reiniciar não põe o banco no ar: quem diz que o processador não
        // está pronto é a verificação do banco.
        Assert.Equal(HealthStatus.Healthy, (await fila.CheckHealthAsync(new HealthCheckContext(), Cancelamento)).Status);
    }

    private static string? Tipo(IReadOnlyDictionary<string, object?> atributos) => atributos["crm.mensagem.tipo"] as string;

    private static string? Resultado(IReadOnlyDictionary<string, object?> atributos) => atributos["crm.mensagem.resultado"] as string;
}
