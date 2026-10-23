using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos;

/// <summary>
/// Ler um agregado pelo id que veio no pedido — ou 404, com a mensagem de
/// sempre. Um recurso lê também agregados de outros: a conversão do lead lê
/// a conta; a venda lê o funil e a conta.
/// </summary>
internal static class Leituras
{
    public static async Task<Versionado<Conta>> Conta(RepositorioDeContas repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new ContaId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a conta {id}.");

    public static async Task<Versionado<Lead>> Lead(RepositorioDeLeads repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new LeadId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o lead {id}.");

    public static async Task<Versionado<Funil>> Funil(RepositorioDeFunis repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new FunilId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe o funil {id}.");

    public static async Task<Versionado<Oportunidade>> Oportunidade(RepositorioDeOportunidades repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new OportunidadeId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a oportunidade {id}.");

    public static async Task<Versionado<Atividade>> Atividade(RepositorioDeAtividades repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new AtividadeId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a atividade {id}.");

    public static async Task<Versionado<Proposta>> Proposta(RepositorioDePropostas repositorio, Guid id, CancellationToken cancelamento) =>
        await repositorio.Obter(new PropostaId(id), cancelamento)
        ?? throw new AgregadoNaoEncontrado($"Não existe a proposta {id}.");
}
