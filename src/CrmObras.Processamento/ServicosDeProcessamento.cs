using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using CrmObras.Processamento.Hospedagem;
using CrmObras.Processamento.Planejamento;
using CrmObras.Processamento.Rotinas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrmObras.Processamento;

/// <summary>O registro do processamento em segundo plano na injeção de dependência.</summary>
public static class ServicosDeProcessamento
{
    /// <summary>
    /// A persistência em nome da construtora de cada mensagem, o processador
    /// da caixa de saída, os tratadores, o cliente do planejamento-obras e as
    /// rotinas — e os serviços que os rodam enquanto o host estiver no ar. A
    /// verificação de que a fila está girando, <see cref="FilaGirando"/>, fica
    /// para o host registrar, com as outras verificações de saúde dele.
    /// </summary>
    /// <remarks>
    /// Tratadores e rotinas são encontrados no assembly, como os recursos da
    /// API: um tratador novo entra sem mexer neste registro.
    /// </remarks>
    /// <param name="planejamento">
    /// Onde está o planejamento-obras e como se apresentar a ele. Obrigatório:
    /// a venda ganha só vira obra lá. Configuração que não serve impede o host
    /// de subir.
    /// </param>
    public static IServiceCollection AdicionarProcessamento(
        this IServiceCollection servicos,
        string conexao,
        Action<OpcoesDoPlanejamento> planejamento,
        Action<OpcoesDoProcessamento>? configurar = null)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);
        ArgumentNullException.ThrowIfNull(planejamento);

        // Cada escopo começa sem construtora e assume a da mensagem que reservou.
        servicos.AdicionarPersistencia<ConstrutoraAssumida>(conexao);
        servicos.AddScoped(provedor => (ConstrutoraAssumida)provedor.GetRequiredService<IConstrutoraAtual>());

        servicos.AddScoped<FilaDaCaixaDeSaida>();
        servicos.AddScoped<ManutencaoDoBanco>();
        servicos.AddSingleton<ProcessadorDaCaixaDeSaida>();

        // As métricas da fila e o pulso que diz se ela está girando (ADR-0018).
        servicos.AddMetrics();
        servicos.AddSingleton<MetricasDaCaixaDeSaida>();
        servicos.AddSingleton<PulsoDaFila>();
        servicos.AddSingleton(provedor => new EsperaPorMensagens(conexao, provedor.GetRequiredService<ILogger<EsperaPorMensagens>>()));

        AdicionarPlanejamento(servicos, planejamento);

        foreach (var tipo in typeof(ServicosDeProcessamento).Assembly.GetTypes().Where(tipo => tipo is { IsClass: true, IsAbstract: false }))
        {
            foreach (var tratador in tipo.GetInterfaces().Where(EhTratador))
            {
                servicos.AddScoped(tratador, tipo);
            }

            if (tipo.IsAssignableTo(typeof(IRotina)))
            {
                servicos.AddSingleton(typeof(IRotina), tipo);
            }
        }

        servicos.AddOptions<OpcoesDoProcessamento>().Configure(configurar ?? (_ => { }));
        servicos.AddHostedService<ServicoDaCaixaDeSaida>();
        servicos.AddHostedService<ServicoDasRotinas>();

        return servicos;
    }

    /// <summary>
    /// O cliente HTTP do planejamento, com o endereço e o tempo-limite da
    /// configuração, e o token de serviço com que ele se apresenta (ADR-0017).
    /// </summary>
    private static void AdicionarPlanejamento(IServiceCollection servicos, Action<OpcoesDoPlanejamento> planejamento)
    {
        servicos.AddOptions<OpcoesDoPlanejamento>().Configure(planejamento).ValidateOnStart();
        servicos.AddSingleton<IValidateOptions<OpcoesDoPlanejamento>, ValidacaoDoPlanejamento>();

        servicos.AddSingleton<TokenDeServico>();
        servicos.AddHttpClient<ClienteDoPlanejamento>((provedor, http) =>
        {
            var opcoes = provedor.GetRequiredService<IOptions<OpcoesDoPlanejamento>>().Value;
            http.BaseAddress = opcoes.Endereco;
            http.Timeout = opcoes.TempoLimite;
        });
    }

    private static bool EhTratador(Type contrato) =>
        contrato.IsGenericType && contrato.GetGenericTypeDefinition() == typeof(ITratadorDe<>);
}
