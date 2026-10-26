using CrmObras.Processamento.Hospedagem;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>
/// O host do processador como o Program.cs o monta — a observabilidade, a
/// saúde, a configuração lida do ambiente —, com o relógio do teste e sem a
/// fila nem as rotinas rodando: o pulso só bate quando o teste quer.
/// </summary>
public static class HostDoProcessador
{
    /// <param name="ajuste">O que mais o teste quer no host: um exportador em memória, por exemplo.</param>
    public static WebApplicationFactory<Program> Montar(
        string conexao,
        Uri planejamento,
        TimeProvider relogio,
        Action<IServiceCollection>? ajuste = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testes");
            web.UseSetting("ConnectionStrings:Crm", conexao);
            web.UseSetting("Planejamento:Endereco", planejamento.ToString());
            web.UseSetting("Planejamento:Emissor", AmbienteDoProcessador.Emissor);
            web.UseSetting("Planejamento:Audiencia", AmbienteDoProcessador.Audiencia);
            web.UseSetting("Planejamento:Chave", AmbienteDoProcessador.Chave);
            web.ConfigureTestServices(servicos =>
            {
                foreach (var emSegundoPlano in servicos.Where(Do<ServicoDaCaixaDeSaida>).Concat(servicos.Where(Do<ServicoDasRotinas>)).ToList())
                {
                    servicos.Remove(emSegundoPlano);
                }

                servicos.AddSingleton(relogio);
                ajuste?.Invoke(servicos);
            });
        });

    private static bool Do<TServico>(ServiceDescriptor registro) =>
        registro.ServiceType == typeof(IHostedService) && registro.ImplementationType == typeof(TServico);
}
