using CrmObras.Dominio.Funis;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê funis, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeFunis(ContextoDoBanco contexto, TimeProvider relogio)
{
    private static readonly Dictionary<string, string> SemRestricoes = [];

    private readonly TabelaDeAgregados<RegistroDeFunil> _tabela = new(contexto, relogio, "o funil");

    public Task<Versionado<Funil>?> Obter(FunilId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaFunil(), cancelamento);
    }

    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Funil funil, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(funil);

        return _tabela.Inserir(RegistroDeFunil.De(funil, versao: 1, _tabela.Agora), SemRestricoes, cancelamento);
    }

    /// <param name="versaoLida">A versão em que o funil foi lido.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Funil funil, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(funil);

        var novo = RegistroDeFunil.De(funil, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(novo, versaoLida, colunas => colunas.SetProperty(registro => registro.Nome, novo.Nome), SemRestricoes, cancelamento);
    }

    public Task Remover(FunilId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Apagar(
            id.Valor,
            versaoLida,
            new Dictionary<string, string>
            {
                ["fk_oportunidades_funis"] = $"O funil {id} tem vendas: o funil não sai do banco enquanto houver venda andando ou que andou por ele.",
            },
            cancelamento);
    }

    /// <summary>Os funis da construtora, em ordem de nome, sem abrir documento.</summary>
    public Task<IReadOnlyList<ResumoDoAgregado<FunilId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.Funis.Listar(id => new FunilId(id), cancelamento);
}
