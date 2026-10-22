using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Dominio.Testes.Propostas;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/// <summary>
/// A carteira de uma construtora nos testes: os repositórios, cada um com o
/// seu contexto — como cada requisição da aplicação terá o seu —, e um jeito
/// de gravar uma venda com tudo o que ela pede antes.
/// </summary>
internal sealed class Carteira(BancoDeTestes banco, Guid construtora)
{
    public static readonly DateTimeOffset Segunda = VendasDeTeste.Segunda;

    public Guid Construtora { get; } = construtora;

    public RepositorioDeContas Contas => new(banco.NovoContexto(Construtora), TimeProvider.System);

    public RepositorioDeLeads Leads => new(banco.NovoContexto(Construtora), TimeProvider.System);

    public RepositorioDeFunis Funis => new(banco.NovoContexto(Construtora), TimeProvider.System);

    public RepositorioDeOportunidades Oportunidades => new(banco.NovoContexto(Construtora), TimeProvider.System);

    public RepositorioDeAtividades Atividades => new(banco.NovoContexto(Construtora), TimeProvider.System);

    public RepositorioDePropostas Propostas => new(banco.NovoContexto(Construtora), TimeProvider.System);

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    /// <summary>A conta da Rota Sul, o funil padrão e o galpão, com tudo o que as etapas pedem — gravados nessa ordem.</summary>
    public async Task<(Conta Conta, Funil Funil, Oportunidade Venda)> UmaVendaGravada()
    {
        var conta = VendasDeTeste.RotaSul();
        var funil = Funil.PadraoDaConstrutora();
        var venda = VendasDeTeste.Completa(funil, conta);
        await Contas.Adicionar(conta, Cancelamento);
        await Funis.Adicionar(funil, Cancelamento);
        await Oportunidades.Adicionar(venda, Cancelamento);

        return (conta, funil, venda);
    }

    public async Task<Atividade> UmaAtividadeGravada(Conta conta, Oportunidade? venda = null, string responsavel = "carlos.souza", DateTimeOffset? prevista = null)
    {
        var atividade = new Atividade(
            conta,
            new DadosDaAtividade(TipoDeAtividade.Ligacao, "Retorno da proposta", prevista ?? Segunda.AddDays(3), conta.Contatos[1].Id),
            responsavel,
            Segunda,
            venda);
        await Atividades.Adicionar(atividade, Cancelamento);

        return atividade;
    }

    public async Task<Proposta> UmaPropostaGravada(Oportunidade venda)
    {
        var proposta = PropostasDeTeste.DoGalpao(venda, 4m);
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Segunda.AddDays(1));
        await Propostas.Adicionar(proposta, Cancelamento);

        return proposta;
    }
}
