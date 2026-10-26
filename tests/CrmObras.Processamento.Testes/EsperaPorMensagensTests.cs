using System.Diagnostics;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Hospedagem;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrmObras.Processamento.Testes;

/*
 * O aviso de mensagem nova pelo LISTEN/NOTIFY do PostgreSQL: chega quando a
 * transação que gravou a mensagem é confirmada — e só então —, e sem banco a
 * espera vira o intervalo, sem derrubar ninguém.
 */
public sealed class EsperaPorMensagensTests(BancoDeTestes banco) : TesteComFila(banco)
{
    /// <summary>Uma porta onde não há PostgreSQL: a conexão é recusada, ou não chega a tempo.</summary>
    internal const string BancoForaDoAr = "Host=127.0.0.1;Port=1;Username=ninguem;Password=nada;Timeout=1";

    private EsperaPorMensagens NovaEspera() => new(Banco.ConexaoDoProcessador, NullLogger<EsperaPorMensagens>.Instance);

    [Fact]
    public async Task A_venda_ganha_confirmada_acorda_quem_espera()
    {
        await using var espera = NovaEspera();
        await espera.Preparar(Cancelamento);
        var acordou = espera.Aguardar(TimeSpan.FromMinutes(5), Cancelamento);

        await Vendas().GanharUma();

        Assert.True(await acordou.WaitAsync(TimeSpan.FromSeconds(10), Cancelamento));
    }

    [Fact]
    public async Task A_venda_ganha_desfeita_nao_acorda_ninguem()
    {
        await using var espera = NovaEspera();
        await espera.Preparar(Cancelamento);

        await Vendas().GanharEDesfazer();

        var inicio = Stopwatch.GetTimestamp();
        Assert.False(await espera.Aguardar(TimeSpan.FromMilliseconds(500), Cancelamento));
        Assert.True(Stopwatch.GetElapsedTime(inicio) >= TimeSpan.FromMilliseconds(400), "Voltou antes do intervalo.");
    }

    [Fact]
    public async Task Sem_banco_a_espera_vira_o_intervalo()
    {
        using var registro = new RegistroEmMemoria();
        await using var espera = new EsperaPorMensagens(BancoForaDoAr, registro.Para<EsperaPorMensagens>());

        await espera.Preparar(Cancelamento);
        Assert.False(await espera.Aguardar(TimeSpan.FromMilliseconds(100), Cancelamento));

        Assert.Equal(2, registro.Entradas.Count(entrada => entrada.Nivel == LogLevel.Warning));
    }
}
