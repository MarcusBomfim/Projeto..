using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Testes.Leads;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * Cada repositório grava e lê de volta o agregado inteiro, sobe a versão a
 * cada gravação e responde às consultas de que a agenda, o quadro e a linha
 * do tempo precisam — num PostgreSQL de verdade, com o papel da aplicação.
 */
public sealed class RepositoriosTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    [Fact]
    public async Task A_conta_vai_e_volta_e_a_versao_sobe_a_cada_gravacao()
    {
        var conta = new Conta("Rota Sul", Cnpj.De("11.222.333/0001-81"));
        conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina@rotasul.com.br"), Papel: PapelNaCompra.Decisor));

        var criada = await _carteira.Contas.Adicionar(conta, Cancelamento);
        conta.Renomear("Logística Rota Sul");
        var alterada = await _carteira.Contas.Atualizar(conta, criada, Cancelamento);
        var lida = await _carteira.Contas.Obter(conta.Id, Cancelamento);

        Assert.Equal((1, 2), (criada, alterada));
        Assert.Equal((2, "Logística Rota Sul", (Documento?)Cnpj.De("11222333000181")), (lida?.Versao, lida?.Agregado.Nome, lida?.Agregado.Documento));
        Assert.Equal("Marina Costa", lida?.Agregado.Principal?.Nome);
        Assert.Equal(conta.Id, (await _carteira.Contas.ObterPeloDocumento(Cnpj.De("11222333000181"), Cancelamento))?.Agregado.Id);
    }

    [Fact]
    public async Task Remover_tira_do_banco()
    {
        var conta = new Conta("Rota Sul");
        await _carteira.Contas.Adicionar(conta, Cancelamento);

        await _carteira.Contas.Remover(conta.Id, 1, Cancelamento);

        Assert.Null(await _carteira.Contas.Obter(conta.Id, Cancelamento));
    }

    [Fact]
    public async Task Lista_as_contas_e_os_funis_pelo_nome_sem_abrir_documento()
    {
        await _carteira.Contas.Adicionar(new Conta("Construtora Beta"), Cancelamento);
        await _carteira.Contas.Adicionar(new Conta("Alfa Engenharia"), Cancelamento);
        await _carteira.Funis.Adicionar(new Funil("Obras públicas", [new("Edital", 5)]), Cancelamento);
        await _carteira.Funis.Adicionar(Funil.PadraoDaConstrutora(), Cancelamento);

        var contas = await _carteira.Contas.Listar(Cancelamento);
        var funis = await _carteira.Funis.Listar(Cancelamento);

        Assert.Equal(["Alfa Engenharia", "Construtora Beta"], contas.Select(conta => conta.Nome));
        Assert.Equal(["Obras públicas", "Vendas de obras"], funis.Select(funil => funil.Nome));
        Assert.All(contas, conta => Assert.Equal(1, conta.Versao));
    }

    [Fact]
    public async Task O_lead_vai_e_volta_e_e_achado_pela_situacao()
    {
        var novo = LeadsDeTeste.DoSite();
        var qualificado = LeadsDeTeste.Qualificado();
        await _carteira.Leads.Adicionar(novo, Cancelamento);
        await _carteira.Leads.Adicionar(qualificado, Cancelamento);

        novo.Descartar(MotivoDoDescarte.SemOrcamento, null, LeadsDeTeste.Segunda.AddDays(1));
        await _carteira.Leads.Atualizar(novo, 1, Cancelamento);

        Assert.Equal(SituacaoDoLead.Descartado, (await _carteira.Leads.Obter(novo.Id, Cancelamento))?.Agregado.Situacao);
        Assert.Equal([qualificado.Id], (await _carteira.Leads.NaSituacao(SituacaoDoLead.Qualificado, Cancelamento)).Select(lead => lead.Agregado.Id));
        Assert.Equal([novo.Id], (await _carteira.Leads.NaSituacao(SituacaoDoLead.Descartado, Cancelamento)).Select(lead => lead.Agregado.Id));
    }

    [Fact]
    public async Task A_venda_vai_e_volta_com_as_passagens_e_e_achada_pelo_funil_pelo_vendedor_e_pela_conta()
    {
        var (conta, funil, venda) = await _carteira.UmaVendaGravada();
        venda.MoverPara(funil, funil.Abertas[3].Id, Carteira.Segunda.AddDays(5));
        venda.TransferirPara("ana.ribeiro");
        await _carteira.Oportunidades.Atualizar(venda, 1, Cancelamento);
        var ganha = new Oportunidade(conta.Id, "Portaria", funil, "ana.ribeiro", Carteira.Segunda);
        ganha.Ganhar(funil, Dinheiro.Reais(80_000m), Carteira.Segunda.AddDays(1));
        await _carteira.Oportunidades.Adicionar(ganha, Cancelamento);

        var lida = await _carteira.Oportunidades.Obter(venda.Id, Cancelamento);

        Assert.Equal(venda.Passagens, lida?.Agregado.Passagens);
        Assert.Equal((2, "ana.ribeiro", venda.ValorEstimado), (lida?.Versao, lida?.Agregado.Responsavel, lida?.Agregado.ValorEstimado));
        Assert.Equal([venda.Id, ganha.Id], (await _carteira.Oportunidades.DoFunil(funil.Id, Cancelamento)).Select(lida => lida.Agregado.Id));
        Assert.Equal([venda.Id], (await _carteira.Oportunidades.AbertasDoResponsavel("ana.ribeiro", Cancelamento)).Select(lida => lida.Agregado.Id));
        Assert.Equal(2, (await _carteira.Oportunidades.DaConta(conta.Id, Cancelamento)).Count);
    }

    [Fact]
    public async Task O_funil_vai_e_volta_com_as_arquivadas()
    {
        var funil = Funil.PadraoDaConstrutora();
        await _carteira.Funis.Adicionar(funil, Cancelamento);
        funil.ArquivarEtapa(funil.Abertas[1].Id);
        funil.Renomear("Obras privadas");

        await _carteira.Funis.Atualizar(funil, 1, Cancelamento);
        var lido = await _carteira.Funis.Obter(funil.Id, Cancelamento);

        Assert.Equal("Obras privadas", lido?.Agregado.Nome);
        Assert.Equal([funil.Arquivadas[0].Id], lido?.Agregado.Arquivadas.Select(etapa => etapa.Id));
        Assert.Equal("Obras privadas", Assert.Single(await _carteira.Funis.Listar(Cancelamento)).Nome);
    }

    [Fact]
    public async Task As_atividades_da_agenda_sao_as_pendentes_do_vendedor_e_as_das_vendas_dele()
    {
        var (conta, _, venda) = await _carteira.UmaVendaGravada();
        var minha = await _carteira.UmaAtividadeGravada(conta, prevista: Carteira.Segunda.AddDays(4));
        var doEngenheiro = await _carteira.UmaAtividadeGravada(conta, venda, "joao.engenheiro", Carteira.Segunda.AddDays(2));
        var deOutro = await _carteira.UmaAtividadeGravada(conta, responsavel: "ana.ribeiro");
        var feita = await _carteira.UmaAtividadeGravada(conta);
        feita.Concluir("Feita", Carteira.Segunda.AddDays(1));
        await _carteira.Atividades.Atualizar(feita, 1, Cancelamento);

        var daAgenda = await _carteira.Atividades.PendentesParaAAgenda("carlos.souza", [venda.Id], Cancelamento);

        Assert.Equal([doEngenheiro.Id, minha.Id], daAgenda.Select(lida => lida.Agregado.Id));
        Assert.Equal(4, (await _carteira.Atividades.DaConta(conta.Id, Cancelamento)).Count);
        Assert.Equal("Feita", (await _carteira.Atividades.Obter(feita.Id, Cancelamento))?.Agregado.Resultado);
        Assert.NotEqual(deOutro.Id, minha.Id);
    }

    [Fact]
    public async Task A_proposta_vai_e_volta_com_as_versoes()
    {
        var (conta, _, venda) = await _carteira.UmaVendaGravada();
        var proposta = await _carteira.UmaPropostaGravada(venda);
        proposta.Recusar("Caro", Carteira.Segunda.AddDays(2));
        proposta.NovaVersao();
        await _carteira.Propostas.Atualizar(proposta, 1, Cancelamento);

        var lida = await _carteira.Propostas.Obter(proposta.Id, Cancelamento);

        Assert.Equal(
            [(1, Dominio.Propostas.SituacaoDaVersao.Recusada), (2, Dominio.Propostas.SituacaoDaVersao.Rascunho)],
            lida?.Agregado.Versoes.Select(versao => (versao.Numero, versao.Situacao)));
        Assert.Equal([proposta.Id], (await _carteira.Propostas.DaOportunidade(venda.Id, Cancelamento)).Select(lida => lida.Agregado.Id));
        Assert.Equal([proposta.Id], (await _carteira.Propostas.DaConta(conta.Id, Cancelamento)).Select(lida => lida.Agregado.Id));
    }
}
