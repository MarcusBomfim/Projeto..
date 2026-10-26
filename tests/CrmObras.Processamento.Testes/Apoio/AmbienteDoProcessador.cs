using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Planejamento;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>
/// O processamento montado como o host o monta — com o papel do processador
/// no banco dos testes e o planejamento de mentira no lugar do de verdade —,
/// mas com o relógio do teste e, se o teste quiser, tratadores a mais. Os
/// serviços em segundo plano ficam registrados e parados: o teste chama o
/// processador quando quer.
/// </summary>
public sealed class AmbienteDoProcessador : IAsyncDisposable
{
    public const string Emissor = "crm-obras-testes";

    public const string Audiencia = "planejamento-obras-testes";

    /// <summary>A chave do token de serviço, nova a cada execução: só vale aqui.</summary>
    public static readonly string Chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly ServiceProvider _provedor;

    /// <param name="planejamento">Onde está o planejamento; o teste pode ainda mudar a configuração dele.</param>
    /// <param name="conexao">Outro banco no lugar do dos testes — um que não está no ar, por exemplo.</param>
    public AmbienteDoProcessador(
        BancoDeTestes banco,
        TimeProvider relogio,
        Uri planejamento,
        Action<IServiceCollection>? registrar = null,
        Action<OpcoesDoPlanejamento>? configurarPlanejamento = null,
        string? conexao = null)
    {
        ArgumentNullException.ThrowIfNull(banco);

        var servicos = new ServiceCollection()
            .AddLogging()
            .AddSingleton(relogio);

        servicos.AdicionarProcessamento(conexao ?? banco.ConexaoDoProcessador, opcoes =>
        {
            Configurar(opcoes, planejamento);
            configurarPlanejamento?.Invoke(opcoes);
        });
        registrar?.Invoke(servicos);

        _provedor = servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public ProcessadorDaCaixaDeSaida Processador => _provedor.GetRequiredService<ProcessadorDaCaixaDeSaida>();

    /// <summary>A configuração do planejamento dos testes, apontando para o de mentira.</summary>
    public static void Configurar(OpcoesDoPlanejamento opcoes, Uri planejamento)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes.Endereco = planejamento;
        opcoes.Emissor = Emissor;
        opcoes.Audiencia = Audiencia;
        opcoes.Chave = Chave;
        opcoes.TempoLimite = TimeSpan.FromSeconds(10);
    }

    public AsyncServiceScope NovoEscopo() => _provedor.CreateAsyncScope();

    /// <summary>Um serviço que o processamento não registra, montado com o que ele registra: a verificação de saúde, por exemplo.</summary>
    public T Montar<T>() => ActivatorUtilities.CreateInstance<T>(_provedor);

    /// <summary>
    /// O que um instrumento do processamento medir daqui em diante — só deste
    /// ambiente, e não de outro que esteja no ar no mesmo processo.
    /// </summary>
    public MetricCollector<T> Metrica<T>(string instrumento)
        where T : struct =>
        new(_provedor.GetRequiredService<IMeterFactory>(), TelemetriaDoProcessamento.Nome, instrumento);

    public ValueTask DisposeAsync() => _provedor.DisposeAsync();
}
