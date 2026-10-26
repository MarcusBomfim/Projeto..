using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>
/// Grava e lê oportunidades, com concorrência otimista (ADR-0012). A conta e
/// o funil são outros agregados: a oportunidade guarda o id deles, e o banco
/// confere que existem.
/// </summary>
/// <remarks>
/// Os fatos da venda — a venda ganha — vão para a caixa de saída na mesma
/// transação da gravação, e saem do agregado depois dela (ADR-0016).
/// </remarks>
public sealed class RepositorioDeOportunidades(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeOportunidade> _tabela = new(contexto, relogio, "a oportunidade");

    public Task<Versionado<Oportunidade>?> Obter(OportunidadeId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaOportunidade(), cancelamento);
    }

    /// <summary>As vendas de um funil — para o quadro e os relatórios —, da mais antiga.</summary>
    public Task<IReadOnlyList<Versionado<Oportunidade>>> DoFunil(FunilId funil, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(funil);

        return Consultar(registros => registros.Where(registro => registro.FunilId == funil.Valor), cancelamento);
    }

    /// <summary>As vendas abertas de um vendedor — para a agenda dele.</summary>
    public Task<IReadOnlyList<Versionado<Oportunidade>>> AbertasDoResponsavel(string responsavel, CancellationToken cancelamento = default) =>
        Consultar(
            registros => registros.Where(registro => registro.Responsavel == responsavel && registro.Situacao == SituacaoDaOportunidade.Aberta),
            cancelamento);

    /// <summary>As vendas de uma conta — para a linha do tempo do cliente.</summary>
    public Task<IReadOnlyList<Versionado<Oportunidade>>> DaConta(ContaId conta, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return Consultar(registros => registros.Where(registro => registro.ContaId == conta.Valor), cancelamento);
    }

    /// <remarks>A conta e o funil precisam estar gravados antes.</remarks>
    /// <returns>A versão gravada: 1.</returns>
    public async Task<int> Adicionar(Oportunidade oportunidade, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(oportunidade);

        var versao = await _tabela.Inserir(
            RegistroDeOportunidade.De(oportunidade, versao: 1, _tabela.Agora),
            Referencias(oportunidade),
            cancelamento,
            oportunidade.Eventos);

        oportunidade.DescartarEventos();

        return versao;
    }

    /// <param name="versaoLida">A versão em que a oportunidade foi lida.</param>
    /// <returns>A versão nova.</returns>
    public async Task<int> Atualizar(Oportunidade oportunidade, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(oportunidade);

        // A conta e o funil de uma venda não mudam; o título, o vendedor e a situação, sim.
        var nova = RegistroDeOportunidade.De(oportunidade, versaoLida + 1, _tabela.Agora);

        var versao = await _tabela.Atualizar(
            nova,
            versaoLida,
            colunas => colunas
                .SetProperty(registro => registro.Titulo, nova.Titulo)
                .SetProperty(registro => registro.Responsavel, nova.Responsavel)
                .SetProperty(registro => registro.Situacao, nova.Situacao),
            Referencias(oportunidade),
            cancelamento,
            oportunidade.Eventos);

        oportunidade.DescartarEventos();

        return versao;
    }

    public Task Remover(OportunidadeId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var emUso = $"A oportunidade {id} tem atividades ou propostas: elas são a história da venda, e a venda não sai do banco enquanto existirem.";

        return _tabela.Apagar(
            id.Valor,
            versaoLida,
            new Dictionary<string, string> { ["fk_atividades_oportunidades"] = emUso, ["fk_propostas_oportunidades"] = emUso },
            cancelamento);
    }

    private Task<IReadOnlyList<Versionado<Oportunidade>>> Consultar(
        Func<IQueryable<RegistroDeOportunidade>, IQueryable<RegistroDeOportunidade>> filtro,
        CancellationToken cancelamento) =>
        _tabela.Consultar(
            registros => filtro(registros).OrderBy(registro => registro.CriadoEm).ThenBy(registro => registro.Id),
            registro => registro.ParaOportunidade(),
            cancelamento);

    private static Dictionary<string, string> Referencias(Oportunidade oportunidade) => new()
    {
        ["fk_oportunidades_contas"] = $"A conta {oportunidade.Conta} não está gravada: grave-a antes da venda.",
        ["fk_oportunidades_funis"] = $"O funil {oportunidade.Funil} não está gravado: grave-o antes da venda.",
    };
}
