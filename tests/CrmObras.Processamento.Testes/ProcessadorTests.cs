using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using CrmObras.Infraestrutura.Persistencia.Repositorios;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrmObras.Processamento.Testes;

/*
 * O processador da caixa de saída contra o banco de verdade: cada mensagem
 * tratada uma vez, em nome da construtora dela; a falha desfaz o que os
 * tratadores gravaram e a mensagem volta mais tarde, com espera crescente,
 * até a fila desistir; dois processadores dividem a fila sem tratar nada
 * duas vezes (ADR-0017).
 */
public sealed class ProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private readonly Anotacoes _anotacoes = new();

    private AmbienteDoProcessador ProcessamentoQueAnota(Action<IServiceCollection>? registrar = null) =>
        Processamento(servicos =>
        {
            servicos.AddSingleton(_anotacoes).TratarTambemCom<TratadorQueAnota>();
            registrar?.Invoke(servicos);
        });

    private async Task<MensagemNoBanco> UnicaMensagemDe(Oportunidade venda) =>
        Assert.Single(await CaixaNoBanco.DoAgregado(Banco, venda.Id.Valor));

    private async Task<string> TituloNoBanco(Oportunidade venda) =>
        (await new RepositorioDeOportunidades(Banco.NovoContexto(Construtora), Relogio).Obter(venda.Id, Cancelamento))!.Agregado.Titulo;

    [Fact]
    public async Task Cada_mensagem_e_tratada_uma_vez_e_marcada_como_tratada()
    {
        var galpao = await Vendas().GanharUma();
        var portaria = await Vendas().GanharUma("Portaria da fábrica", 180_000m);
        await using var processamento = ProcessamentoQueAnota();

        Assert.Equal(2, await processamento.Processador.ProcessarPendentes(Cancelamento));
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal(
            [(galpao.Id.Valor, "Galpão de 1.200 m² em Jundiaí", 2_350_000m), (portaria.Id.Valor, "Portaria da fábrica", 180_000m)],
            _anotacoes.Todas.Select(anotacao => (anotacao.Mensagem.Oportunidade, anotacao.Mensagem.Titulo, anotacao.Mensagem.ValorFechado)));
        Assert.All(
            await CaixaNoBanco.Todas(Banco),
            mensagem => Assert.Equal(((DateTimeOffset?)Relogio.Agora, 0, (string?)null), (mensagem.TratadaEm, mensagem.Tentativas, mensagem.UltimoErro)));
    }

    [Fact]
    public async Task Cada_mensagem_e_tratada_em_nome_da_construtora_dela()
    {
        var alfa = Guid.CreateVersion7();
        var beta = Guid.CreateVersion7();
        var daAlfa = await Vendas(alfa).GanharUma();
        var daBeta = await Vendas(beta).GanharUma();
        await using var processamento = ProcessamentoQueAnota();

        await processamento.Processador.ProcessarPendentes(Cancelamento);

        // No código e no banco: o filtro e a segurança por linha valem para o
        // tratamento como valem para um pedido.
        Assert.Equal(
            [(daAlfa.Id.Valor, alfa, alfa.ToString()), (daBeta.Id.Valor, beta, beta.ToString())],
            _anotacoes.Todas.Select(anotacao => (anotacao.Mensagem.Oportunidade, anotacao.Construtora!.Value, anotacao.ConstrutoraNoBanco)));
    }

    [Fact]
    public async Task O_que_o_tratador_grava_fica_junto_com_a_marca_de_tratada()
    {
        var venda = await Vendas().GanharUma();
        await using var processamento = Processamento(servicos => servicos.TratarTambemCom<TratadorQueGrava>());

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal("Galpão de 1.200 m² em Jundiaí" + TratadorQueGrava.Marca, await TituloNoBanco(venda));
        Assert.NotNull((await UnicaMensagemDe(venda)).TratadaEm);
    }

    [Fact]
    public async Task Falha_desfaz_o_que_os_tratadores_gravaram_e_a_mensagem_volta_depois_da_espera()
    {
        var venda = await Vendas().GanharUma();
        await using var processamento = Processamento(servicos => servicos
            .TratarTambemCom<TratadorQueGrava>()
            .AddSingleton(new Falhas(vezes: 1))
            .TratarTambemCom<TratadorQueFalha>());

        // O que grava vem antes do que falha: a gravação dele é desfeita.
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = await UnicaMensagemDe(venda);
        Assert.Equal("Galpão de 1.200 m² em Jundiaí", await TituloNoBanco(venda));
        Assert.Equal(
            (1, Relogio.Agora + TimeSpan.FromSeconds(30), (DateTimeOffset?)null, "InvalidOperationException: Falha de propósito."),
            (mensagem.Tentativas, mensagem.ProximaTentativaEm, mensagem.TratadaEm, mensagem.UltimoErro));

        // Antes da hora a mensagem não volta; na hora, volta — e passa.
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));
        Relogio.Adiantar(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal("Galpão de 1.200 m² em Jundiaí" + TratadorQueGrava.Marca, await TituloNoBanco(venda));
        Assert.Equal(Relogio.Agora, (await UnicaMensagemDe(venda)).TratadaEm);
    }

    [Fact]
    public async Task Depois_de_dez_falhas_a_fila_desiste_da_mensagem()
    {
        var venda = await Vendas().GanharUma();
        using var registro = new RegistroEmMemoria();
        await using var processamento = Processamento(servicos => servicos
            .AddLogging(log => log.AddProvider(registro))
            .AddSingleton(new Falhas(vezes: int.MaxValue))
            .TratarTambemCom<TratadorQueFalha>());
        var esperas = new List<int>();

        for (var tentativa = 1; tentativa <= FilaDaCaixaDeSaida.TentativasAntesDeDesistir; tentativa++)
        {
            Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

            var mensagem = await UnicaMensagemDe(venda);
            esperas.Add((int)(mensagem.ProximaTentativaEm - Relogio.Agora).TotalSeconds);
            Relogio.Agora = mensagem.ProximaTentativaEm;
        }

        var abandonada = await UnicaMensagemDe(venda);
        Assert.Equal([30, 60, 120, 240, 480, 960, 1_920, 3_600, 3_600, 3_600], esperas);
        Assert.Equal((10, (DateTimeOffset?)null), (abandonada.Tentativas, abandonada.TratadaEm));
        Assert.NotNull(abandonada.AbandonadaEm);

        // Abandonada, não volta mais — nem um dia depois.
        Relogio.Adiantar(TimeSpan.FromDays(1));
        Assert.Equal(0, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var doProcessador = registro.Entradas.Where(entrada => entrada.Categoria == typeof(ProcessadorDaCaixaDeSaida).FullName).ToList();
        Assert.Equal(9, doProcessador.Count(entrada => entrada.Nivel == LogLevel.Warning));
        Assert.Contains("foi abandonada", Assert.Single(doProcessador, entrada => entrada.Nivel == LogLevel.Error).Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mensagem_de_tipo_desconhecido_espera_uma_versao_que_a_conheca()
    {
        // Numa implantação aos poucos, a API nova pode gravar um fato novo
        // antes de o processador novo subir: o velho não descarta — tenta de
        // novo mais tarde.
        var id = await CaixaNoBanco.GravarCrua(Banco, Construtora, "venda-perdida", Relogio.Agora);
        await using var processamento = Processamento();

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = Assert.Single(await CaixaNoBanco.Todas(Banco));
        Assert.Equal(
            (id, 1, (DateTimeOffset?)null, (DateTimeOffset?)null, "InvalidOperationException: Tipo de mensagem desconhecido: venda-perdida."),
            (mensagem.Id, mensagem.Tentativas, mensagem.TratadaEm, mensagem.AbandonadaEm, mensagem.UltimoErro));
    }

    [Fact]
    public async Task Dois_processadores_nunca_tratam_a_mesma_mensagem()
    {
        const int Quantas = 12;
        var ganhas = new List<Guid>();

        for (var i = 0; i < Quantas; i++)
        {
            ganhas.Add((await Vendas().GanharUma($"Galpão {i + 1}")).Id.Valor);
        }

        await using var um = ProcessamentoQueAnota(servicos => servicos.TratarTambemCom<TratadorDemorado>());
        await using var outro = ProcessamentoQueAnota(servicos => servicos.TratarTambemCom<TratadorDemorado>());

        var tratadas = await Task.WhenAll(
            um.Processador.ProcessarPendentes(Cancelamento),
            outro.Processador.ProcessarPendentes(Cancelamento));

        Assert.Equal(Quantas, tratadas.Sum());
        Assert.Equal(ganhas.Order(), _anotacoes.Todas.Select(anotacao => anotacao.Mensagem.Oportunidade).Order());
        Assert.Equal(Quantas, Planejamento.Abertos.Count);
    }

    [Fact]
    public async Task Parar_no_meio_devolve_a_mensagem_a_fila_sem_contar_falha()
    {
        var venda = await Vendas().GanharUma();
        using var parar = new CancellationTokenSource();

        await using (var parando = Processamento(servicos => servicos
            .TratarTambemCom<TratadorQueGrava>()
            .AddSingleton(parar)
            .TratarTambemCom<TratadorQueCancela>()))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parando.Processador.ProcessarPendentes(parar.Token));
        }

        var devolvida = await UnicaMensagemDe(venda);
        Assert.Equal((0, (DateTimeOffset?)null, (string?)null), (devolvida.Tentativas, devolvida.TratadaEm, devolvida.UltimoErro));
        Assert.Equal("Galpão de 1.200 m² em Jundiaí", await TituloNoBanco(venda));

        // O próximo processador a encontra, pronta.
        await using var processamento = Processamento();
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));
    }
}
