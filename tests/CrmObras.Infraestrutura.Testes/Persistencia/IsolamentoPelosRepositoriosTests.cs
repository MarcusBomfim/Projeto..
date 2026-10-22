using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * Para outra construtora, o agregado não existe: ler, alterar ou apagar o
 * agregado de outra dá o mesmo resultado de um id que nunca existiu. Dizer
 * "existe, mas não é seu" já seria vazar (ADR-0011).
 */
public sealed class IsolamentoPelosRepositoriosTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Carteira _alfa = new(banco, Guid.CreateVersion7());

    private readonly Carteira _beta = new(banco, Guid.CreateVersion7());

    [Fact]
    public async Task A_conta_de_outra_construtora_nao_existe_para_esta()
    {
        var conta = new Conta("Cliente da Alfa");
        await _alfa.Contas.Adicionar(conta, Cancelamento);

        var atualizar = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _beta.Contas.Atualizar(conta, 1, Cancelamento));
        var remover = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _beta.Contas.Remover(conta.Id, 1, Cancelamento));

        Assert.Null(await _beta.Contas.Obter(conta.Id, Cancelamento));
        Assert.Empty(await _beta.Contas.Listar(Cancelamento));
        Assert.Equal($"Não existe a conta {conta.Id}.", atualizar.Message);
        Assert.Equal(atualizar.Message, remover.Message);
        Assert.NotNull(await _alfa.Contas.Obter(conta.Id, Cancelamento));
    }

    [Fact]
    public async Task A_venda_nao_aponta_para_a_conta_de_outra_construtora()
    {
        var conta = VendasDeTeste.RotaSul();
        await _alfa.Contas.Adicionar(conta, Cancelamento);
        var funil = Funil.PadraoDaConstrutora();
        await _beta.Funis.Adicionar(funil, Cancelamento);

        var erro = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _beta.Oportunidades.Adicionar(VendasDeTeste.DoGalpao(funil, conta), Cancelamento));

        Assert.Equal($"A conta {conta.Id} não está gravada: grave-a antes da venda.", erro.Message);
    }

    [Fact]
    public async Task O_mesmo_documento_pode_ser_cliente_de_duas_construtoras()
    {
        var cnpj = Dominio.Identificacao.Cnpj.De("11.222.333/0001-81");

        await _alfa.Contas.Adicionar(new Conta("Rota Sul", cnpj), Cancelamento);
        await _beta.Contas.Adicionar(new Conta("Rota Sul", cnpj), Cancelamento);

        Assert.NotEqual(
            (await _alfa.Contas.ObterPeloDocumento(cnpj, Cancelamento))?.Agregado.Id,
            (await _beta.Contas.ObterPeloDocumento(cnpj, Cancelamento))?.Agregado.Id);
    }
}
