using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Dominio.Testes.Propostas;
using CrmObras.Infraestrutura.Persistencia;
using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * O que o PostgreSQL recusa vira exceção com nome — e mensagem para gente.
 * Quem chama trata o que aconteceu, e não um código SQL.
 */
public sealed class RecusasDoBancoTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    [Fact]
    public async Task O_mesmo_agregado_nao_entra_duas_vezes()
    {
        var conta = new Conta("Rota Sul");
        await _carteira.Contas.Adicionar(conta, Cancelamento);

        var erro = await Assert.ThrowsAsync<AgregadoJaExiste>(() => _carteira.Contas.Adicionar(conta, Cancelamento));

        Assert.Equal($"Já existe a conta {conta.Id}.", erro.Message);
    }

    [Fact]
    public async Task O_documento_identifica_a_conta_tambem_no_banco()
    {
        await _carteira.Contas.Adicionar(new Conta("Rota Sul", Cnpj.De("11.222.333/0001-81")), Cancelamento);
        var outra = new Conta("Rota Sul Logística");
        await _carteira.Contas.Adicionar(outra, Cancelamento);
        outra.InformarDocumento(Cnpj.De("11.222.333/0001-81"));

        var nova = await Assert.ThrowsAsync<AgregadoJaExiste>(() => _carteira.Contas.Adicionar(new Conta("Rota Sul S/A", Cnpj.De("11222333000181")), Cancelamento));
        var alterada = await Assert.ThrowsAsync<AgregadoJaExiste>(() => _carteira.Contas.Atualizar(outra, 1, Cancelamento));

        Assert.Equal("Já existe uma conta com o documento 11.222.333/0001-81: o documento identifica o cliente.", nova.Message);
        Assert.Equal(nova.Message, alterada.Message);
    }

    [Fact]
    public async Task A_venda_precisa_da_conta_e_do_funil_gravados()
    {
        var conta = VendasDeTeste.RotaSul();
        var funil = Funil.PadraoDaConstrutora();
        var venda = VendasDeTeste.DoGalpao(funil, conta);

        var semConta = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Oportunidades.Adicionar(venda, Cancelamento));
        await _carteira.Contas.Adicionar(conta, Cancelamento);
        var semFunil = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Oportunidades.Adicionar(venda, Cancelamento));

        Assert.Equal($"A conta {conta.Id} não está gravada: grave-a antes da venda.", semConta.Message);
        Assert.Equal($"O funil {funil.Id} não está gravado: grave-o antes da venda.", semFunil.Message);
    }

    [Fact]
    public async Task A_atividade_e_a_proposta_precisam_da_venda_gravada()
    {
        var conta = VendasDeTeste.RotaSul();
        await _carteira.Contas.Adicionar(conta, Cancelamento);
        var venda = VendasDeTeste.DoGalpao(Funil.PadraoDaConstrutora(), conta);
        var atividade = new Atividade(conta, new DadosDaAtividade(TipoDeAtividade.Visita, "Visita", Carteira.Segunda), "carlos.souza", Carteira.Segunda, venda);

        var daAtividade = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Atividades.Adicionar(atividade, Cancelamento));
        var daProposta = await Assert.ThrowsAsync<AgregadoNaoEncontrado>(() => _carteira.Propostas.Adicionar(PropostasDeTeste.DoGalpao(venda), Cancelamento));

        Assert.Equal($"A oportunidade {venda.Id} não está gravada: grave-a antes da atividade.", daAtividade.Message);
        Assert.Equal($"A oportunidade {venda.Id} não está gravada: grave-a antes da proposta.", daProposta.Message);
    }

    [Fact]
    public async Task O_que_tem_historia_nao_sai_do_banco()
    {
        var (conta, funil, venda) = await _carteira.UmaVendaGravada();
        await _carteira.UmaAtividadeGravada(conta, venda);

        var aConta = await Assert.ThrowsAsync<AgregadoEmUso>(() => _carteira.Contas.Remover(conta.Id, 1, Cancelamento));
        var oFunil = await Assert.ThrowsAsync<AgregadoEmUso>(() => _carteira.Funis.Remover(funil.Id, 1, Cancelamento));
        var aVenda = await Assert.ThrowsAsync<AgregadoEmUso>(() => _carteira.Oportunidades.Remover(venda.Id, 1, Cancelamento));

        Assert.Equal($"A conta {conta.Id} tem vendas, atividades ou propostas: elas são a história do cliente, e a conta não sai do banco enquanto existirem.", aConta.Message);
        Assert.Equal($"O funil {funil.Id} tem vendas: o funil não sai do banco enquanto houver venda andando ou que andou por ele.", oFunil.Message);
        Assert.Equal($"A oportunidade {venda.Id} tem atividades ou propostas: elas são a história da venda, e a venda não sai do banco enquanto existirem.", aVenda.Message);
    }

    [Fact]
    public async Task Documento_estragado_no_banco_vira_documento_invalido_com_o_id()
    {
        var conta = new Conta("Rota Sul");
        await _carteira.Contas.Adicionar(conta, Cancelamento);
        await Executar(
            """update crm.contas set documento = jsonb_set(documento, '{nome}', '""') where id = @p0""",
            conta.Id.Valor);

        var erro = await Assert.ThrowsAsync<DocumentoInvalido>(() => _carteira.Contas.Obter(conta.Id, Cancelamento));

        Assert.Equal($"A conta {conta.Id} não se lê: Falta o nome da conta.", erro.Message);
    }

    [Fact]
    public async Task Venda_estragada_no_banco_diz_o_que_as_regras_nao_produzem()
    {
        var (_, _, venda) = await _carteira.UmaVendaGravada();
        await Executar(
            """update crm.oportunidades set documento = jsonb_set(documento, '{situacao}', '"Ganha"') where id = @p0""",
            venda.Id.Valor);

        var erro = await Assert.ThrowsAsync<DocumentoInvalido>(() => _carteira.Oportunidades.Obter(venda.Id, Cancelamento));

        Assert.StartsWith($"A oportunidade {venda.Id} não se lê: A oportunidade Galpão de 1.200 m² em Jundiaí está gravada num estado", erro.Message, StringComparison.Ordinal);
    }

    private async Task Executar(string sql, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

        await comando.ExecuteNonQueryAsync(Cancelamento);
    }
}
