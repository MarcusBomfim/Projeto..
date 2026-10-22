using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>
/// Grava e lê propostas, com concorrência otimista (ADR-0012). A venda e a
/// conta são outros agregados: a proposta guarda o id delas, e o banco
/// confere que existem.
/// </summary>
public sealed class RepositorioDePropostas(ContextoDoBanco contexto, TimeProvider relogio)
{
    private static readonly Dictionary<string, string> SemRestricoes = [];

    private readonly TabelaDeAgregados<RegistroDeProposta> _tabela = new(contexto, relogio, "a proposta");

    public Task<Versionado<Proposta>?> Obter(PropostaId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaProposta(), cancelamento);
    }

    /// <summary>As propostas de uma venda.</summary>
    public Task<IReadOnlyList<Versionado<Proposta>>> DaOportunidade(OportunidadeId oportunidade, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(oportunidade);

        return Consultar(registros => registros.Where(registro => registro.OportunidadeId == oportunidade.Valor), cancelamento);
    }

    /// <summary>As propostas de uma conta — para a linha do tempo do cliente.</summary>
    public Task<IReadOnlyList<Versionado<Proposta>>> DaConta(ContaId conta, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return Consultar(registros => registros.Where(registro => registro.ContaId == conta.Valor), cancelamento);
    }

    /// <remarks>A venda e a conta precisam estar gravadas antes.</remarks>
    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Proposta proposta, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        return _tabela.Inserir(
            RegistroDeProposta.De(proposta, versao: 1, _tabela.Agora),
            new Dictionary<string, string>
            {
                ["fk_propostas_oportunidades"] = $"A oportunidade {proposta.Oportunidade} não está gravada: grave-a antes da proposta.",
                ["fk_propostas_contas"] = $"A conta {proposta.Conta} não está gravada: grave-a antes da proposta.",
            },
            cancelamento);
    }

    /// <param name="versaoLida">A versão em que a proposta foi lida.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Proposta proposta, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        // A venda e a conta de uma proposta não mudam; só o documento.
        return _tabela.Atualizar(RegistroDeProposta.De(proposta, versaoLida + 1, _tabela.Agora), versaoLida, outrasColunas: null, SemRestricoes, cancelamento);
    }

    public Task Remover(PropostaId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(id.Valor, versaoLida, SemRestricoes, cancelamento);
    }

    private Task<IReadOnlyList<Versionado<Proposta>>> Consultar(
        Func<IQueryable<RegistroDeProposta>, IQueryable<RegistroDeProposta>> filtro,
        CancellationToken cancelamento) =>
        _tabela.Consultar(
            registros => filtro(registros).OrderBy(registro => registro.CriadoEm).ThenBy(registro => registro.Id),
            registro => registro.ParaProposta(),
            cancelamento);
}
