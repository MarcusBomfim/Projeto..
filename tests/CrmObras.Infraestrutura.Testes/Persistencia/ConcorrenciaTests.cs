using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * Ninguém grava por cima do que não viu (ADR-0012): atualizar e remover
 * pedem a versão lida, e o banco confere e grava num comando só.
 */
public sealed class ConcorrenciaTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    [Fact]
    public async Task Quem_grava_a_partir_de_uma_versao_velha_e_recusado()
    {
        var conta = new Conta("Rota Sul");
        await _carteira.Contas.Adicionar(conta, Cancelamento);
        var daAna = (await _carteira.Contas.Obter(conta.Id, Cancelamento))!;
        var doBruno = (await _carteira.Contas.Obter(conta.Id, Cancelamento))!;

        daAna.Agregado.Renomear("Logística Rota Sul");
        await _carteira.Contas.Atualizar(daAna.Agregado, daAna.Versao, Cancelamento);
        doBruno.Agregado.Renomear("Rota Sul Ltda");
        var erro = await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() => _carteira.Contas.Atualizar(doBruno.Agregado, doBruno.Versao, Cancelamento));

        Assert.Equal(
            $"A conta {conta.Id} mudou desde a versão 1: está na versão 2. Leia de novo e refaça a alteração.",
            erro.Message);
        Assert.Equal(2, erro.VersaoAtual);
        Assert.Equal("Logística Rota Sul", (await _carteira.Contas.Obter(conta.Id, Cancelamento))?.Agregado.Nome);
    }

    [Fact]
    public async Task Das_dez_gravacoes_ao_mesmo_tempo_a_partir_da_mesma_versao_so_uma_passa()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();

        var gravacoes = Enumerable.Range(1, 10).Select(async vez =>
        {
            var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
            lida.Agregado.Estimar(Dinheiro.Reais(1_000m * vez));

            try
            {
                await _carteira.Oportunidades.Atualizar(lida.Agregado, 1, Cancelamento);

                return true;
            }
            catch (ConflitoDeConcorrencia)
            {
                return false;
            }
        });

        var resultados = await Task.WhenAll(gravacoes);

        Assert.Single(resultados, passou => passou);
        Assert.Equal(2, (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))?.Versao);
        Assert.NotNull(funil);
    }

    [Fact]
    public async Task Remover_a_partir_de_uma_versao_velha_e_recusado()
    {
        var conta = new Conta("Rota Sul");
        await _carteira.Contas.Adicionar(conta, Cancelamento);
        conta.Renomear("Logística Rota Sul");
        await _carteira.Contas.Atualizar(conta, 1, Cancelamento);

        var erro = await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() => _carteira.Contas.Remover(conta.Id, 1, Cancelamento));

        Assert.Equal(2, erro.VersaoAtual);
        Assert.NotNull(await _carteira.Contas.Obter(conta.Id, Cancelamento));
    }

    [Fact]
    public async Task O_que_nao_existe_nao_e_conflito()
    {
        var conta = new Conta("Rota Sul");

        var atualizar = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Contas.Atualizar(conta, 1, Cancelamento));
        var remover = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Contas.Remover(conta.Id, 1, Cancelamento));

        Assert.Equal($"Não existe a conta {conta.Id}.", atualizar.Message);
        Assert.Equal(atualizar.Message, remover.Message);
        Assert.Null(await _carteira.Contas.Obter(conta.Id, Cancelamento));
    }
}
