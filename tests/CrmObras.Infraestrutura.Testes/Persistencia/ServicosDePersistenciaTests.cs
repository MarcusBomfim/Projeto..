using CrmObras.Dominio.Clientes;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

public sealed class ServicosDePersistenciaTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cada_escopo_trabalha_em_nome_da_construtora_dele()
    {
        var alfa = Guid.CreateVersion7();
        await using var servicos = new ServiceCollection()
            .AdicionarPersistencia<ConstrutoraDoEscopo>(banco.ConexaoDaAplicacao)
            .BuildServiceProvider();
        var conta = new Conta("Cliente da Alfa");

        await using (var escopo = servicos.CreateAsyncScope())
        {
            ((ConstrutoraDoEscopo)escopo.ServiceProvider.GetRequiredService<IConstrutoraAtual>()).Id = alfa;
            await escopo.ServiceProvider.GetRequiredService<RepositorioDeContas>().Adicionar(conta, Cancelamento);
        }

        await using (var semConstrutora = servicos.CreateAsyncScope())
        {
            Assert.Null(await semConstrutora.ServiceProvider.GetRequiredService<RepositorioDeContas>().Obter(conta.Id, Cancelamento));
        }

        await using var daAlfa = servicos.CreateAsyncScope();
        ((ConstrutoraDoEscopo)daAlfa.ServiceProvider.GetRequiredService<IConstrutoraAtual>()).Id = alfa;
        Assert.NotNull(await daAlfa.ServiceProvider.GetRequiredService<RepositorioDeContas>().Obter(conta.Id, Cancelamento));
    }

    [Fact]
    public void Registra_os_seis_repositorios_e_o_relogio()
    {
        using var servicos = new ServiceCollection()
            .AdicionarPersistencia<ConstrutoraDoEscopo>("Host=localhost;Database=crm_obras")
            .BuildServiceProvider();
        using var escopo = servicos.CreateScope();

        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDeContas>());
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDeLeads>());
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDeFunis>());
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDeOportunidades>());
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDeAtividades>());
        Assert.NotNull(escopo.ServiceProvider.GetRequiredService<RepositorioDePropostas>());
        Assert.Same(TimeProvider.System, escopo.ServiceProvider.GetRequiredService<TimeProvider>());
    }

    /// <summary>A construtora de um escopo, como a API vai tirá-la do token: definida depois que o escopo nasce.</summary>
    private sealed class ConstrutoraDoEscopo : IConstrutoraAtual
    {
        public Guid? Id { get; set; }
    }
}
