using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>
/// Grava e lê atividades, com concorrência otimista (ADR-0012). A conta e a
/// venda são outros agregados: a atividade guarda o id deles, e o banco
/// confere que existem.
/// </summary>
public sealed class RepositorioDeAtividades(ContextoDoBanco contexto, TimeProvider relogio)
{
    private static readonly Dictionary<string, string> SemRestricoes = [];

    private readonly TabelaDeAgregados<RegistroDeAtividade> _tabela = new(contexto, relogio, "a atividade");

    public Task<Versionado<Atividade>?> Obter(AtividadeId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaAtividade(), cancelamento);
    }

    /// <summary>
    /// O que a agenda de um vendedor precisa (ADR-0006): as pendentes dele, e
    /// as pendentes das vendas dele — o próximo passo pode ser de outro.
    /// </summary>
    public Task<IReadOnlyList<Versionado<Atividade>>> PendentesParaAAgenda(
        string responsavel,
        IEnumerable<OportunidadeId> vendas,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(vendas);

        List<Guid> ids = [.. vendas.Select(venda => venda.Valor)];

        return Consultar(
            registros => registros.Where(registro => registro.Situacao == SituacaoDaAtividade.Pendente
                && (registro.Responsavel == responsavel || (registro.OportunidadeId != null && ids.Contains(registro.OportunidadeId.Value)))),
            cancelamento);
    }

    /// <summary>As atividades de uma conta — para a linha do tempo do cliente.</summary>
    public Task<IReadOnlyList<Versionado<Atividade>>> DaConta(ContaId conta, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return Consultar(registros => registros.Where(registro => registro.ContaId == conta.Valor), cancelamento);
    }

    /// <remarks>A conta, e a venda se houver, precisam estar gravadas antes.</remarks>
    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Atividade atividade, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(atividade);

        return _tabela.Inserir(
            RegistroDeAtividade.De(atividade, versao: 1, _tabela.Agora),
            new Dictionary<string, string>
            {
                ["fk_atividades_contas"] = $"A conta {atividade.Conta} não está gravada: grave-a antes da atividade.",
                ["fk_atividades_oportunidades"] = $"A oportunidade {atividade.Oportunidade} não está gravada: grave-a antes da atividade.",
            },
            cancelamento);
    }

    /// <param name="versaoLida">A versão em que a atividade foi lida.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Atividade atividade, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(atividade);

        var nova = RegistroDeAtividade.De(atividade, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(
            nova,
            versaoLida,
            colunas => colunas
                .SetProperty(registro => registro.Responsavel, nova.Responsavel)
                .SetProperty(registro => registro.Situacao, nova.Situacao)
                .SetProperty(registro => registro.PrevistaPara, nova.PrevistaPara),
            SemRestricoes,
            cancelamento);
    }

    public Task Remover(AtividadeId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, SemRestricoes, cancelamento);
    }

    private Task<IReadOnlyList<Versionado<Atividade>>> Consultar(
        Func<IQueryable<RegistroDeAtividade>, IQueryable<RegistroDeAtividade>> filtro,
        CancellationToken cancelamento) =>
        _tabela.Consultar(
            registros => filtro(registros).OrderBy(registro => registro.PrevistaPara).ThenBy(registro => registro.Id),
            registro => registro.ParaAtividade(),
            cancelamento);
}
