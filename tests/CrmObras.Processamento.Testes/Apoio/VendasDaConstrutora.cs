using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Infraestrutura.Persistencia.Repositorios;
using CrmObras.Infraestrutura.Testes.Persistencia;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>
/// As vendas de uma construtora, gravadas e ganhas como a API faz: pelos
/// repositórios, com o papel da aplicação e o relógio do teste — e, com o
/// ganho, a mensagem na caixa de saída.
/// </summary>
public sealed class VendasDaConstrutora(BancoDeTestes banco, Guid construtora, TimeProvider relogio)
{
    public const decimal Valor = 2_350_000m;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private RepositorioDeOportunidades Oportunidades() => new(banco.NovoContexto(construtora), relogio);

    /// <summary>Uma venda nova da Rota Sul, ganha pelo valor dado.</summary>
    public async Task<Oportunidade> GanharUma(string titulo = "Galpão de 1.200 m² em Jundiaí", decimal valor = Valor)
    {
        var (funil, venda) = await GravarUma(titulo);
        var lida = (await Oportunidades().Obter(venda.Id, Cancelamento))!;
        lida.Agregado.Ganhar(funil, Dinheiro.Reais(valor), VendasDeTeste.Segunda.AddDays(20));

        await Oportunidades().Atualizar(lida.Agregado, lida.Versao, Cancelamento);

        return lida.Agregado;
    }

    /// <summary>Ganha uma venda numa transação que é desfeita no fim: nada fica, nem a mensagem.</summary>
    public async Task GanharEDesfazer()
    {
        var (funil, venda) = await GravarUma("Portaria da fábrica");
        await using var contexto = banco.NovoContexto(construtora);
        var oportunidades = new RepositorioDeOportunidades(contexto, relogio);
        var lida = (await oportunidades.Obter(venda.Id, Cancelamento))!;
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);

        lida.Agregado.Ganhar(funil, Dinheiro.Reais(Valor), VendasDeTeste.Segunda.AddDays(20));
        await oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento);

        await transacao.RollbackAsync(Cancelamento);
    }

    private async Task<(Funil Funil, Oportunidade Venda)> GravarUma(string titulo)
    {
        var conta = VendasDeTeste.RotaSul();
        var funil = Funil.PadraoDaConstrutora();
        var venda = new Oportunidade(conta.Id, titulo, funil, "carlos.souza", VendasDeTeste.Segunda);

        await new RepositorioDeContas(banco.NovoContexto(construtora), relogio).Adicionar(conta, Cancelamento);
        await new RepositorioDeFunis(banco.NovoContexto(construtora), relogio).Adicionar(funil, Cancelamento);
        await Oportunidades().Adicionar(venda, Cancelamento);

        return (funil, venda);
    }
}
