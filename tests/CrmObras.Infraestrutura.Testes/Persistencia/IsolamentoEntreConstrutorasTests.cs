using CrmObras.Dominio.Clientes;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Registros;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * Cada construtora só enxerga o que é dela, por duas barreiras (ADR-0011): o
 * filtro do código e a segurança por linha do banco. Cada uma é testada sem a
 * outra — a do banco com o filtro do código desligado; a do código
 * conectada como superusuário, que passa por cima do banco.
 */
public sealed class IsolamentoEntreConstrutorasTests(BancoDeTestes banco)
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-3));

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _alfa = Guid.CreateVersion7();

    private readonly Guid _beta = Guid.CreateVersion7();

    [Fact]
    public async Task Cada_construtora_so_ve_as_contas_dela()
    {
        var daAlfa = await Gravar(_alfa, "Construtora Alfa — cliente");
        var daBeta = await Gravar(_beta, "Construtora Beta — cliente");

        Assert.Equal([daAlfa], await IdsDasContas(banco.NovoContexto(_alfa)));
        Assert.Equal([daBeta], await IdsDasContas(banco.NovoContexto(_beta)));
    }

    [Fact]
    public async Task A_seguranca_do_banco_isola_mesmo_sem_o_filtro_do_codigo()
    {
        var daAlfa = await Gravar(_alfa, "Cliente da Alfa");
        await Gravar(_beta, "Cliente da Beta");

        Assert.Equal([daAlfa], await IdsDasContas(banco.NovoContexto(_alfa), comFiltro: false));
    }

    [Fact]
    public async Task O_filtro_do_codigo_isola_mesmo_sem_a_seguranca_do_banco()
    {
        var daAlfa = await Gravar(_alfa, "Cliente da Alfa");
        var daBeta = await Gravar(_beta, "Cliente da Beta");

        // O dono do banco é superusuário: a segurança por linha não o alcança.
        var comoDono = ContextoDoBanco.Criar(banco.Conexao, new ConstrutoraFixa(_alfa));
        var semFiltro = await IdsDasContas(ContextoDoBanco.Criar(banco.Conexao, new ConstrutoraFixa(_alfa)), comFiltro: false);

        Assert.Equal([daAlfa], await IdsDasContas(comoDono));
        Assert.Contains(daBeta, semFiltro);
    }

    [Fact]
    public async Task Sem_construtora_nada_se_ve_e_nada_se_grava()
    {
        await Gravar(_alfa, "Cliente da Alfa");
        await using var contexto = banco.NovoContextoSemConstrutora();

        var erro = Assert.Throws<InvalidOperationException>(() => contexto.ConstrutoraQueGrava);

        Assert.Empty(await IdsDasContas(banco.NovoContextoSemConstrutora(), comFiltro: false));
        Assert.StartsWith("Sem construtora, nada se grava", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nao_se_grava_em_nome_de_outra_construtora()
    {
        await using var contexto = banco.NovoContexto(_alfa);
        var registro = RegistroDeConta.De(new Conta("Cliente da Beta"), versao: 1, Agora);
        registro.ConstrutoraId = _beta;
        contexto.Contas.Add(registro);

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync(Cancelamento));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (erro.InnerException as PostgresException)?.SqlState);
        Assert.Empty(await IdsDasContas(banco.NovoContexto(_beta)));
    }

    [Fact]
    public async Task A_conexao_que_volta_ao_pool_nao_leva_a_construtora_para_o_pedido_seguinte()
    {
        /*
         * Sem a limpeza do pool, a sessão do PostgreSQL atravessa pedidos:
         * um parâmetro definido por um pedido continuaria lá no seguinte. A
         * construtora é definida a cada abertura — inclusive a ausência dela.
         */
        var conexao = new NpgsqlConnectionStringBuilder(banco.ConexaoDaAplicacao) { NoResetOnClose = true, MaxPoolSize = 1 }.ConnectionString;
        await Gravar(_alfa, "Cliente da Alfa");

        Assert.NotEmpty(await IdsDasContas(ContextoDoBanco.Criar(conexao, new ConstrutoraFixa(_alfa)), comFiltro: false));
        Assert.Empty(await IdsDasContas(ContextoDoBanco.Criar(conexao, ConstrutoraFixa.Nenhuma), comFiltro: false));
    }

    [Fact]
    public void A_construtora_nao_e_o_guid_vazio()
    {
        var erro = Assert.Throws<ArgumentException>(() => new ConstrutoraFixa(Guid.Empty));

        Assert.StartsWith("A construtora não pode ser o Guid vazio", erro.Message, StringComparison.Ordinal);
        Assert.Null(ConstrutoraFixa.Nenhuma.Id);
    }

    private async Task<Guid> Gravar(Guid construtora, string nome)
    {
        await using var contexto = banco.NovoContexto(construtora);
        var registro = RegistroDeConta.De(new Conta(nome), versao: 1, Agora);
        registro.ConstrutoraId = contexto.ConstrutoraQueGrava;
        contexto.Contas.Add(registro);
        await contexto.SaveChangesAsync(Cancelamento);

        return registro.Id;
    }

    /// <summary>As contas que o contexto enxerga entre as deste teste — o banco é compartilhado com os outros testes.</summary>
    private async Task<List<Guid>> IdsDasContas(ContextoDoBanco contexto, bool comFiltro = true)
    {
        await using (contexto)
        {
            var contas = comFiltro ? contexto.Contas : contexto.Contas.IgnoreQueryFilters([ContextoDoBanco.FiltroDaConstrutora]);

            return await contas
                .Where(conta => conta.ConstrutoraId == _alfa || conta.ConstrutoraId == _beta)
                .Select(conta => conta.Id)
                .ToListAsync(Cancelamento);
        }
    }
}
