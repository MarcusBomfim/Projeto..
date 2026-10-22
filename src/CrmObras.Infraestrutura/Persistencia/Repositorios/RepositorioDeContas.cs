using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Identificacao;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia.Repositorios;

/// <summary>Grava e lê contas, com concorrência otimista (ADR-0012).</summary>
public sealed class RepositorioDeContas(ContextoDoBanco contexto, TimeProvider relogio)
{
    private readonly TabelaDeAgregados<RegistroDeConta> _tabela = new(contexto, relogio, "a conta");

    public Task<Versionado<Conta>?> Obter(ContaId id, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return _tabela.Obter(id.Valor, registro => registro.ParaConta(), cancelamento);
    }

    /// <summary>A conta do CNPJ ou do CPF — o documento identifica o cliente (ADR-0002).</summary>
    public async Task<Versionado<Conta>?> ObterPeloDocumento(Documento documento, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(documento);

        var contas = await _tabela.Consultar(
            registros => registros.Where(registro => registro.CnpjOuCpf == documento.Numero),
            registro => registro.ParaConta(),
            cancelamento);

        return contas.SingleOrDefault();
    }

    /// <returns>A versão gravada: 1.</returns>
    public Task<int> Adicionar(Conta conta, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return _tabela.Inserir(RegistroDeConta.De(conta, versao: 1, _tabela.Agora), Restricoes(conta), cancelamento);
    }

    /// <param name="versaoLida">A versão em que a conta foi lida.</param>
    /// <returns>A versão nova.</returns>
    public Task<int> Atualizar(Conta conta, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(conta);

        var nova = RegistroDeConta.De(conta, versaoLida + 1, _tabela.Agora);

        return _tabela.Atualizar(
            nova,
            versaoLida,
            colunas => colunas
                .SetProperty(registro => registro.Nome, nova.Nome)
                .SetProperty(registro => registro.CnpjOuCpf, nova.CnpjOuCpf),
            Restricoes(conta),
            cancelamento);
    }

    public Task Remover(ContaId id, int versaoLida, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var emUso = $"A conta {id} tem vendas, atividades ou propostas: elas são a história do cliente, e a conta não sai do banco enquanto existirem.";

        return _tabela.Apagar(
            id.Valor,
            versaoLida,
            new Dictionary<string, string>
            {
                ["fk_oportunidades_contas"] = emUso,
                ["fk_atividades_contas"] = emUso,
                ["fk_propostas_contas"] = emUso,
            },
            cancelamento);
    }

    /// <summary>As contas da construtora, em ordem de nome, sem abrir documento.</summary>
    public Task<IReadOnlyList<ResumoDoAgregado<ContaId>>> Listar(CancellationToken cancelamento = default) =>
        contexto.Contas.Listar(id => new ContaId(id), cancelamento);

    private static Dictionary<string, string> Restricoes(Conta conta) => new()
    {
        ["ux_contas_construtora_id_cnpj_ou_cpf"] = $"Já existe uma conta com o documento {conta.Documento}: o documento identifica o cliente.",
    };
}
