using CrmObras.Dominio.Clientes;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Idempotencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * A chave de idempotência e a operação na mesma transação: ou as duas ficam,
 * ou nenhuma. E o mesmo pedido, repetido, recebe a mesma resposta sem rodar
 * de novo — mesmo chegando enquanto o primeiro ainda roda (ADR-0013).
 */
public sealed class ChavesDeIdempotenciaTests(BancoDeTestes banco)
{
    private static readonly RespostaGuardada Criado = new(
        201,
        new Dictionary<string, string> { ["Location"] = "/api/contas/0199", ["ETag"] = "\"1\"" },
        """{"id":"0199","nome":"Rota Sul"}""");

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private static string NovaChave() => Guid.NewGuid().ToString();

    private ChavesDeIdempotencia Chaves(Guid construtora) => new(banco.NovoContexto(construtora), TimeProvider.System);

    [Fact]
    public async Task Pedido_novo_roda_uma_vez_e_o_repetido_recebe_a_mesma_resposta()
    {
        var chave = NovaChave();
        var operacao = new Operacao(Criado);

        var primeiro = await Chaves(_construtora).Atender(chave, "POST /api/contas 9f2c", operacao.Executar, Cancelamento);
        var repetido = await Chaves(_construtora).Atender(chave, "POST /api/contas 9f2c", operacao.Executar, Cancelamento);

        Assert.Equal((false, true, 1), (primeiro.Repetido, repetido.Repetido, operacao.Vezes));
        Assert.Equal((Criado.Status, Criado.Corpo), (repetido.Resposta.Status, repetido.Resposta.Corpo));
        Assert.Equal(Criado.Cabecalhos, repetido.Resposta.Cabecalhos);
    }

    [Fact]
    public async Task Chave_usada_em_outro_pedido_e_recusada_sem_rodar_nada()
    {
        var chave = NovaChave();
        await Chaves(_construtora).Atender(chave, "o primeiro pedido", new Operacao(Criado).Executar, Cancelamento);
        var outra = new Operacao(Criado);

        var erro = await Assert.ThrowsAsync<ChaveDeIdempotenciaReutilizada>(() =>
            Chaves(_construtora).Atender(chave, "outro pedido", outra.Executar, Cancelamento));

        Assert.Equal(0, outra.Vezes);
        Assert.Contains(chave, erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operacao_que_falha_libera_a_chave()
    {
        var chave = NovaChave();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Chaves(_construtora).Atender(chave, "pedido", _ => throw new InvalidOperationException("Falhou no meio."), Cancelamento));

        var deNovo = new Operacao(Criado);
        var atendido = await Chaves(_construtora).Atender(chave, "pedido", deNovo.Executar, Cancelamento);

        Assert.Equal((false, 1), (atendido.Repetido, deNovo.Vezes));
    }

    [Fact]
    public async Task Resposta_de_erro_nao_fica_guardada()
    {
        var chave = NovaChave();
        await Chaves(_construtora).Atender(chave, "pedido", new Operacao(Criado with { Status = 422 }).Executar, Cancelamento);

        var deNovo = new Operacao(Criado);
        var atendido = await Chaves(_construtora).Atender(chave, "pedido", deNovo.Executar, Cancelamento);

        Assert.Equal((false, 1, Criado.Status), (atendido.Repetido, deNovo.Vezes, atendido.Resposta.Status));
    }

    [Fact]
    public async Task A_chave_e_o_que_a_operacao_gravou_ficam_juntos_ou_nenhum()
    {
        // A operação grava pelo mesmo contexto da chave: entra na transação dela.
        await using var contexto = banco.NovoContexto(_construtora);
        var chaves = new ChavesDeIdempotencia(contexto, TimeProvider.System);
        var contas = new RepositorioDeContas(contexto, TimeProvider.System);
        var desfeita = new Conta("Desfeita");
        var gravada = new Conta("Gravada");

        await chaves.Atender(
            NovaChave(),
            "pedido que falha",
            async cancelamento =>
            {
                await contas.Adicionar(desfeita, cancelamento);

                return Criado with { Status = 500 };
            },
            Cancelamento);

        await chaves.Atender(
            NovaChave(),
            "pedido que dá certo",
            async cancelamento =>
            {
                await contas.Adicionar(gravada, cancelamento);

                return Criado;
            },
            Cancelamento);

        var lidas = new RepositorioDeContas(banco.NovoContexto(_construtora), TimeProvider.System);

        Assert.Null(await lidas.Obter(desfeita.Id, Cancelamento));
        Assert.NotNull(await lidas.Obter(gravada.Id, Cancelamento));
    }

    [Fact]
    public async Task Pedido_repetido_enquanto_o_primeiro_roda_espera_e_recebe_a_resposta_dele()
    {
        var chave = NovaChave();
        var reservou = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primeiro = Chaves(_construtora).Atender(
            chave,
            "pedido",
            async _ =>
            {
                reservou.SetResult();
                await liberar.Task;

                return Criado;
            },
            Cancelamento);

        // O primeiro reservou a chave e está no meio da operação.
        await reservou.Task;

        var segundaOperacao = new Operacao(Criado with { Status = 500 });
        var segundo = Chaves(_construtora).Atender(chave, "pedido", segundaOperacao.Executar, Cancelamento);

        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelamento);
        Assert.False(segundo.IsCompleted, "O segundo pedido deveria esperar o primeiro terminar.");

        liberar.SetResult();
        var doPrimeiro = await primeiro;
        var doSegundo = await segundo;

        Assert.Equal((false, true, 0), (doPrimeiro.Repetido, doSegundo.Repetido, segundaOperacao.Vezes));
        Assert.Equal(Criado.Status, doSegundo.Resposta.Status);
    }

    [Fact]
    public async Task Cada_construtora_tem_as_suas_chaves()
    {
        var chave = NovaChave();

        var deUma = await Chaves(_construtora).Atender(chave, "pedido de uma", new Operacao(Criado).Executar, Cancelamento);
        var deOutra = await Chaves(Guid.CreateVersion7()).Atender(chave, "pedido de outra", new Operacao(Criado).Executar, Cancelamento);

        Assert.Equal((false, false), (deUma.Repetido, deOutra.Repetido));
    }

    [Fact]
    public async Task O_filtro_do_codigo_separa_as_chaves_mesmo_sem_a_seguranca_do_banco()
    {
        // O dono do banco é superusuário: a segurança por linha não o
        // alcança, e só o filtro separa a chave de uma construtora da mesma
        // chave na outra.
        var chave = NovaChave();
        var outra = Guid.CreateVersion7();
        ChavesDeIdempotencia ComoDono(Guid construtora) => new(ContextoDoBanco.Criar(banco.Conexao, new ConstrutoraFixa(construtora)), TimeProvider.System);

        await ComoDono(_construtora).Atender(chave, "pedido de uma", new Operacao(Criado).Executar, Cancelamento);
        await ComoDono(outra).Atender(chave, "pedido de outra", new Operacao(Criado).Executar, Cancelamento);
        var repetido = await ComoDono(outra).Atender(chave, "pedido de outra", new Operacao(Criado).Executar, Cancelamento);

        Assert.True(repetido.Repetido);
    }

    [Fact]
    public async Task A_reserva_exige_uma_transacao_aberta()
    {
        await using var contexto = banco.NovoContexto(_construtora);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ChavesDeIdempotencia(contexto, TimeProvider.System).Reservar(NovaChave(), "pedido", Cancelamento));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Chave_em_branco_e_recusada(string chave)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            Chaves(_construtora).Atender(chave, "pedido", new Operacao(Criado).Executar, Cancelamento));
    }

    [Fact]
    public async Task Chave_longa_demais_e_recusada()
    {
        var longa = new string('x', ChavesDeIdempotencia.TamanhoMaximoDaChave + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Chaves(_construtora).Atender(longa, "pedido", new Operacao(Criado).Executar, Cancelamento));
    }

    /// <summary>Uma operação que só responde — e conta quantas vezes rodou.</summary>
    private sealed class Operacao(RespostaGuardada resposta)
    {
        public int Vezes { get; private set; }

        public Task<RespostaGuardada> Executar(CancellationToken cancelamento)
        {
            Vezes++;

            return Task.FromResult(resposta);
        }
    }
}
