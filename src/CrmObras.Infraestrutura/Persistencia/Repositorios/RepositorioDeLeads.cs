using CrmObras.Dominio.Leads;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê leads, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeLeads(ContextoDoBanco contexto, TimeProvider relogio)
{
    private static readonly Dictionary<string, string> SemRestricoes = [];

    private readonly TabelaDeAgregados<RegistroDeLead> _tabela = new(contexto, relogio, "o lead");

    public Task<Versionado<Lead>?> Obter(LeadId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaLead(), cancelamento);
    }

    /// <summary>Os leads numa situação — os novos, os em qualificação —, do que chegou primeiro.</summary>
    public Task<IReadOnlyList<Versionado<Lead>>> NaSituacao(SituacaoDoLead situacao, CancellationToken cancelamento = default) =>
        _tabela.Consultar(
            registros => registros.Where(registro => registro.Situacao == situacao).OrderBy(registro => registro.CriadoEm).ThenBy(registro => registro.Id),
            registro => registro.ParaLead(),
            cancelamento);

    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Lead lead, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        return _tabela.Inserir(RegistroDeLead.De(lead, versao: 1, _tabela.Agora), SemRestricoes, cancelamento);
    }

    /// <param name="versaoLida">A versão em que o lead foi lido.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Lead lead, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        var novo = RegistroDeLead.De(lead, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(
            novo,
            versaoLida,
            colunas => colunas
                .SetProperty(registro => registro.Nome, novo.Nome)
                .SetProperty(registro => registro.Situacao, novo.Situacao),
            SemRestricoes,
            cancelamento);
    }

    public Task Remover(LeadId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, SemRestricoes, cancelamento);
    }
}
