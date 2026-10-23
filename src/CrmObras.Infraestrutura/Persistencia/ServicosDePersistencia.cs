using CrmObras.Infraestrutura.Persistencia.Idempotencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>O registro da persistência na injeção de dependência.</summary>
public static class ServicosDePersistencia
{
    /// <summary>
    /// O contexto, os repositórios e as chaves de idempotência, um por
    /// requisição; o relógio do sistema, se ninguém tiver registrado outro.
    /// </summary>
    /// <typeparam name="TConstrutora">
    /// De onde vem a construtora de cada requisição — na API, do token de quem
    /// pediu. Obrigatório: sem ele não há contexto (ADR-0011).
    /// </typeparam>
    public static IServiceCollection AdicionarPersistencia<TConstrutora>(this IServiceCollection servicos, string conexao)
        where TConstrutora : class, IConstrutoraAtual
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);

        servicos.AddScoped<IConstrutoraAtual, TConstrutora>();
        servicos.AddDbContext<ContextoDoBanco>(opcoes => ContextoDoBanco.Configurar(opcoes, conexao));
        servicos.TryAddSingleton(TimeProvider.System);

        servicos.AddScoped<RepositorioDeContas>();
        servicos.AddScoped<RepositorioDeLeads>();
        servicos.AddScoped<RepositorioDeFunis>();
        servicos.AddScoped<RepositorioDeOportunidades>();
        servicos.AddScoped<RepositorioDeAtividades>();
        servicos.AddScoped<RepositorioDePropostas>();
        servicos.AddScoped<ChavesDeIdempotencia>();

        return servicos;
    }
}
