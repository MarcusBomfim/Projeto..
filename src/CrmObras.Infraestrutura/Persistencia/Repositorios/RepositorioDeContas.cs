using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Infraestrutura.Persistencia.Registros;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// As contas que podem ser o cliente do lead — as que a busca de
    /// duplicados vai examinar (ADR-0003): o mesmo documento ou a mesma raiz
    /// de CNPJ, o mesmo nome comparável, e um contato com o mesmo e-mail, o
    /// mesmo telefone ou o mesmo domínio de e-mail de empresa.
    /// </summary>
    /// <remarks>
    /// O banco escolhe as candidatas, pelos índices e pelos contatos dentro
    /// do documento; quem diz que indícios cada uma tem — e em que ordem — é
    /// o domínio, na <see cref="BuscaDeDuplicados"/>.
    /// </remarks>
    public async Task<IReadOnlyList<Conta>> CandidatasADuplicado(Lead lead, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        var documento = lead.Documento?.Numero;
        var raiz = lead.Documento is Cnpj cnpj ? cnpj.Raiz : null;
        var nome = BuscaDeDuplicados.NomeComparavel(lead.NomeDoCliente);
        var email = lead.Email?.Endereco;
        var dominio = lead.Email is { } corporativo && BuscaDeDuplicados.EhDeEmpresa(corporativo) ? corporativo.Dominio : null;
        var telefone = lead.Telefone?.Internacional;

        var candidatas = await _tabela.Consultar(
            registros => registros.FromSql(
                $"""
                select * from crm.contas
                where cnpj_ou_cpf = {documento}
                   or (length(cnpj_ou_cpf) = 14 and left(cnpj_ou_cpf, 8) = {raiz})
                   or nome_comparavel = {nome}
                   or exists (
                       select 1 from jsonb_array_elements(documento -> 'contatos') contato
                       where contato ->> 'email' = {email}
                          or contato ->> 'telefone' = {telefone}
                          or split_part(contato ->> 'email', '@', 2) = {dominio})
                """).OrderBy(registro => registro.Nome).ThenBy(registro => registro.Id),
            registro => registro.ParaConta(),
            cancelamento);

        return [.. candidatas.Select(candidata => candidata.Agregado)];
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
                .SetProperty(registro => registro.CnpjOuCpf, nova.CnpjOuCpf)
                .SetProperty(registro => registro.NomeComparavel, nova.NomeComparavel),
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
