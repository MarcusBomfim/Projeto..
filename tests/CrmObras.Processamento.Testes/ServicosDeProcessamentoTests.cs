using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Processamento.Hospedagem;
using CrmObras.Processamento.Planejamento;
using CrmObras.Processamento.Rotinas;
using CrmObras.Processamento.Testes.Apoio;
using CrmObras.Processamento.Tratadores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrmObras.Processamento.Testes;

public sealed class ServicosDeProcessamentoTests
{
    private const string Conexao = "Host=localhost;Database=crm_obras";

    private static readonly Uri Planejamento = new("http://planejamento-api:8080/");

    private static ServiceProvider Montar() =>
        new ServiceCollection()
            .AddLogging()
            .AdicionarProcessamento(Conexao, opcoes => AmbienteDoProcessador.Configurar(opcoes, Planejamento))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact]
    public async Task Registra_o_tratador_da_venda_ganha_as_rotinas_e_os_servicos_que_os_rodam()
    {
        // Montar não abre conexão nem chama o planejamento: dá para conferir sem nenhum dos dois.
        await using var provedor = Montar();
        await using var escopo = provedor.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;

        Assert.IsType<AberturaDaObra>(Assert.Single(servicos.GetServices<ITratadorDe<MensagemDeVendaGanha>>()));
        Assert.Equal(
            [typeof(LimpezaDaCaixaDeSaida), typeof(LimpezaDasChavesDeIdempotencia)],
            provedor.GetServices<IRotina>().Select(rotina => rotina.GetType()).OrderBy(tipo => tipo.Name));
        Assert.Equal(
            [typeof(ServicoDaCaixaDeSaida), typeof(ServicoDasRotinas)],
            provedor.GetServices<IHostedService>().Select(servico => servico.GetType()));
    }

    [Fact]
    public async Task O_cliente_do_planejamento_vai_ao_endereco_configurado()
    {
        await using var provedor = Montar();

        var http = provedor.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ClienteDoPlanejamento));

        Assert.Equal((Planejamento, TimeSpan.FromSeconds(10)), (http.BaseAddress, http.Timeout));
        Assert.Equal(Planejamento, provedor.GetRequiredService<IOptions<OpcoesDoPlanejamento>>().Value.Endereco);
    }

    [Fact]
    public async Task Cada_escopo_comeca_sem_construtora_e_assume_uma_so()
    {
        await using var provedor = Montar();
        await using var um = provedor.CreateAsyncScope();
        await using var outro = provedor.CreateAsyncScope();
        var construtora = Guid.CreateVersion7();

        var assumida = um.ServiceProvider.GetRequiredService<ConstrutoraAssumida>();
        assumida.Assumir(construtora);

        Assert.Same(assumida, um.ServiceProvider.GetRequiredService<IConstrutoraAtual>());
        Assert.Null(outro.ServiceProvider.GetRequiredService<IConstrutoraAtual>().Id);
        Assert.Throws<InvalidOperationException>(() => assumida.Assumir(Guid.CreateVersion7()));
    }
}
