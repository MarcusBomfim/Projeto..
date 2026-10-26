using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmObras.Processamento.Testes;

/*
 * O processamento num host de verdade, como no CrmObras.Processador: os
 * serviços em segundo plano rodando, acordando com a venda ganha, e
 * sobrevivendo ao banco fora do ar. Sem a configuração do planejamento, o
 * host nem sobe.
 */
public sealed class HostDoProcessadorTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private IHost NovoHost(string conexao, RegistroEmMemoria registro, TimeSpan intervaloDaFila)
    {
        var construtor = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        construtor.Services.AddLogging(log => log.AddProvider(registro).SetMinimumLevel(LogLevel.Debug));
        construtor.Services.AdicionarProcessamento(
            conexao,
            planejamento => AmbienteDoProcessador.Configurar(planejamento, Planejamento.Endereco),
            opcoes => opcoes.IntervaloDaFila = intervaloDaFila);

        return construtor.Build();
    }

    [Fact]
    public async Task O_host_leva_a_venda_ganha_ao_planejamento_assim_que_ela_e_gravada()
    {
        using var registro = new RegistroEmMemoria();
        using var host = NovoHost(Banco.ConexaoDoProcessador, registro, intervaloDaFila: TimeSpan.FromMinutes(10));
        await host.StartAsync(Cancelamento);

        await Ate(
            () => Task.FromResult(registro.Quantas("Fila em dia") > 0),
            TimeSpan.FromSeconds(15),
            "o processador terminar a primeira volta");

        // O host usa o relógio do sistema; a venda também, para a mensagem
        // estar na hora dele.
        await new VendasDaConstrutora(Banco, Construtora, TimeProvider.System).GanharUma();

        // A volta seguinte é daqui a dez minutos: se a obra chega antes, foi
        // o aviso de mensagem nova que acordou o processador.
        await Ate(
            () => Task.FromResult(Planejamento.Abertos.Count == 1),
            TimeSpan.FromSeconds(15),
            "a obra chegar ao planejamento");

        await host.StopAsync(Cancelamento);
    }

    [Fact]
    public async Task Banco_fora_do_ar_nao_derruba_o_host()
    {
        using var registro = new RegistroEmMemoria();
        using var host = NovoHost(EsperaPorMensagensTests.BancoForaDoAr, registro, intervaloDaFila: TimeSpan.FromMilliseconds(100));
        await host.StartAsync(Cancelamento);

        await Ate(
            () => Task.FromResult(registro.Quantas("A volta do processador") >= 2),
            TimeSpan.FromSeconds(30),
            "o processador tentar de novo depois de falhar");

        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested);
        Assert.Equal(2, registro.Quantas("A rotina"));

        await host.StopAsync(Cancelamento);
    }

    [Fact]
    public async Task Sem_a_configuracao_do_planejamento_o_host_nao_sobe_e_diz_o_que_falta()
    {
        var construtor = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        construtor.Services.AdicionarProcessamento(Banco.ConexaoDoProcessador, _ => { });
        using var host = construtor.Build();

        var erro = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Cancelamento));

        Assert.Equal(
            [
                "Planejamento:Endereco é o endereço absoluto da API do planejamento-obras, como http://planejamento-api:8080/.",
                "Planejamento:Emissor é o emissor do token de serviço, que o planejamento-obras aceita para o CRM.",
                "Planejamento:Audiencia é a audiência que a API do planejamento-obras confere no token.",
                "Planejamento:Chave é a chave do token de serviço, em Base64, com 32 bytes ou mais.",
            ],
            erro.Failures);
    }
}
