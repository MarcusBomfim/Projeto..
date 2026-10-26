using System.Collections.Concurrent;
using System.Diagnostics;
using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * A publicação é um passo no rastro de quem gravou o fato, e a mensagem leva
 * o contexto dela para o tratamento continuar o mesmo rastro (ADR-0016).
 * Sozinhos: quem escuta as fontes escuta o processo inteiro, e a gravação de
 * outro teste, ao mesmo tempo, viraria rastro também.
 */
[Collection(CaixaDeSaidaSozinha.Nome)]
public sealed class RastroDaPublicacaoTests(BancoDeTestes banco)
{
    private static readonly ActivitySource Pedido = new("CrmObras.Testes");

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private async Task<Guid> GanharUmaVenda()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        lida.Agregado.Ganhar(funil, Dinheiro.Reais(2_350_000m), Carteira.Segunda.AddDays(20));
        await _carteira.Oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento);

        return venda.Id.Valor;
    }

    [Fact]
    public async Task A_mensagem_leva_o_rastro_da_publicacao_que_continua_o_do_pedido()
    {
        var terminadas = new ConcurrentQueue<Activity>();
        using var ouvinte = new ActivityListener
        {
            ShouldListenTo = fonte => fonte.Name is TelemetriaDaCaixaDeSaida.NomeDaFonte or "CrmObras.Testes",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = terminadas.Enqueue,
        };
        ActivitySource.AddActivityListener(ouvinte);
        ActivityContext pedido;
        Guid venda;

        using (var atividade = Pedido.StartActivity("pedido de teste"))
        {
            pedido = atividade!.Context;
            venda = await GanharUmaVenda();
        }

        var publicacao = Assert.Single(terminadas, atividade => atividade.DisplayName == "publicar venda-ganha");
        Assert.Equal(
            (ActivityKind.Producer, pedido.TraceId, pedido.SpanId),
            (publicacao.Kind, publicacao.TraceId, publicacao.ParentSpanId));
        Assert.Equal(
            ("caixa_de_saida", "send", "venda-ganha", _carteira.Construtora.ToString()),
            (publicacao.GetTagItem("messaging.system"), publicacao.GetTagItem("messaging.operation.type"), publicacao.GetTagItem("messaging.destination.name"), publicacao.GetTagItem("crm.construtora.id")));

        var mensagem = Assert.Single(await CaixaNoBanco.DoAgregado(banco, venda));
        Assert.Equal(publicacao.Id, mensagem.ContextoDoRastreio);
        Assert.Equal(mensagem.Id.ToString(), publicacao.GetTagItem("messaging.message.id"));
    }

    [Fact]
    public async Task Sem_ninguem_rastreando_a_mensagem_vai_sem_rastro()
    {
        var venda = await GanharUmaVenda();

        Assert.Null(Assert.Single(await CaixaNoBanco.DoAgregado(banco, venda)).ContextoDoRastreio);
    }
}
