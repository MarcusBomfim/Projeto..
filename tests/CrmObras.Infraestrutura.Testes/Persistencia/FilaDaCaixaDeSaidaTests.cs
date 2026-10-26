using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Os testes que olham a caixa de saída inteira. A fila reserva a próxima
/// mensagem de qualquer construtora: com outros testes gravando ao mesmo
/// tempo, a próxima seria a deles. Estes rodam sozinhos, depois dos outros,
/// e começam com a caixa vazia.
/// </summary>
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class CaixaDeSaidaSozinha
{
    public const string Nome = "a caixa de saída, sem ninguém gravando junto";
}

/*
 * A fila do lado de quem processa (ADR-0016), com o papel do processador:
 * reserva a próxima mensagem que está na hora — travada, e em nome da
 * construtora dela, só até o fim da transação —, marca a tratada, conta a
 * falha com espera que dobra até uma hora e desiste na décima. Dois
 * processadores nunca pegam a mesma.
 */
[Collection(CaixaDeSaidaSozinha.Nome)]
public sealed class FilaDaCaixaDeSaidaTests(BancoDeTestes banco) : IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    private readonly Guid _construtora = Guid.CreateVersion7();

    private readonly RelogioParado _relogio = new(Agora);

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Sem banco, o teste que precisa dele é pulado ao pedir a conexão.
        if (banco.Disponivel)
        {
            await CaixaNoBanco.Esvaziar(banco);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Uma fila como o processador a tem: o papel dele, e um escopo que começa sem construtora.</summary>
    private (ContextoDoBanco Contexto, FilaDaCaixaDeSaida Fila, ConstrutoraAssumida Construtora) NovaFila()
    {
        var construtora = new ConstrutoraAssumida();
        var contexto = ContextoDoBanco.Criar(banco.ConexaoDoProcessador, construtora);

        return (contexto, new FilaDaCaixaDeSaida(contexto, construtora, _relogio), construtora);
    }

    private static string VendaGanhaEmJson(Guid oportunidade) =>
        $$"""{"oportunidade":"{{oportunidade}}","conta":"{{Guid.CreateVersion7()}}","titulo":"Galpão de 1.200 m² em Jundiaí","valorFechado":2350000,"ganhaEm":"2026-11-08T09:00:00-03:00"}""";

    [Fact]
    public async Task Reserva_a_mensagem_que_esta_na_hora_e_assume_a_construtora_dela()
    {
        var oportunidade = Guid.CreateVersion7();
        var id = await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora, conteudo: VendaGanhaEmJson(oportunidade));
        var (contexto, fila, construtora) = NovaFila();
        await using var _ = contexto;

        // A mesma conexão do começo ao fim, para ver o que fica nela depois da transação.
        await contexto.Database.OpenConnectionAsync(Cancelamento);
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);

        var reservada = await fila.ReservarProxima(Cancelamento);

        Assert.NotNull(reservada);
        Assert.Equal((id, _construtora, 0, Agora), (reservada.Id, reservada.Construtora, reservada.Tentativas, reservada.OcorridaEm));
        Assert.Equal(_construtora, construtora.Id);
        Assert.Equal(oportunidade, Assert.IsType<MensagemDeVendaGanha>(reservada.Ler()).Oportunidade);

        // Dali em diante, o banco vê o que a construtora da mensagem vê — e só nesta transação.
        Assert.Equal(_construtora.ToString(), await Parametro(contexto));
        await transacao.CommitAsync(Cancelamento);
        Assert.Equal(string.Empty, await Parametro(contexto));
    }

    [Fact]
    public async Task So_entrega_o_que_esta_pendente_e_na_hora_do_mais_antigo_ao_mais_novo()
    {
        var tipo = CatalogoDeMensagens.Tipos.VendaGanha;
        var depois = await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, Agora - TimeSpan.FromMinutes(1));
        var antes = await CaixaNoBanco.GravarCrua(banco, Guid.CreateVersion7(), tipo, Agora - TimeSpan.FromMinutes(2));
        await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, Agora + TimeSpan.FromSeconds(1));
        await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, Agora - TimeSpan.FromHours(1), tratadaEm: Agora);
        await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, Agora - TimeSpan.FromHours(1), abandonadaEm: Agora);

        var entregues = new List<Guid>();

        while (true)
        {
            var (contexto, fila, _) = NovaFila();
            await using var __ = contexto;
            await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);

            if (await fila.ReservarProxima(Cancelamento) is not { } reservada)
            {
                break;
            }

            entregues.Add(reservada.Id);
            await fila.MarcarTratada(reservada.Id, Cancelamento);
            await transacao.CommitAsync(Cancelamento);
        }

        Assert.Equal([antes, depois], entregues);
    }

    [Fact]
    public async Task Dois_processadores_nunca_reservam_a_mesma_mensagem()
    {
        var uma = await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora - TimeSpan.FromMinutes(2));
        var outra = await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora - TimeSpan.FromMinutes(1));
        var (contextoDoPrimeiro, primeiro, _) = NovaFila();
        var (contextoDoSegundo, segundo, _) = NovaFila();
        var (contextoDoTerceiro, terceiro, _) = NovaFila();
        await using var a = contextoDoPrimeiro;
        await using var b = contextoDoSegundo;
        await using var c = contextoDoTerceiro;
        await using var transacaoDoPrimeiro = await contextoDoPrimeiro.Database.BeginTransactionAsync(Cancelamento);
        await using var transacaoDoSegundo = await contextoDoSegundo.Database.BeginTransactionAsync(Cancelamento);
        await using var transacaoDoTerceiro = await contextoDoTerceiro.Database.BeginTransactionAsync(Cancelamento);

        // Cada um pula a que está travada pelo outro, sem esperar por ela.
        Assert.Equal(uma, (await primeiro.ReservarProxima(Cancelamento))?.Id);
        Assert.Equal(outra, (await segundo.ReservarProxima(Cancelamento))?.Id);
        Assert.Null(await terceiro.ReservarProxima(Cancelamento));

        // A transação do primeiro acaba sem marcar a dela, e ela volta para a fila.
        await transacaoDoPrimeiro.RollbackAsync(Cancelamento);
        Assert.Equal(uma, (await terceiro.ReservarProxima(Cancelamento))?.Id);
    }

    [Fact]
    public async Task Reservar_fora_de_uma_transacao_e_engano()
    {
        var (contexto, fila, _) = NovaFila();
        await using var _ = contexto;

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => fila.ReservarProxima(Cancelamento));

        Assert.Equal("A reserva vale até o fim da transação; abra uma antes de reservar.", erro.Message);
    }

    [Fact]
    public async Task A_falha_conta_a_tentativa_guarda_o_erro_e_marca_quando_tentar_de_novo()
    {
        var id = await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora);
        var (contexto, fila, _) = NovaFila();
        await using var _ = contexto;
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);
        var reservada = (await fila.ReservarProxima(Cancelamento))!;

        var desistiu = await fila.RegistrarFalha(reservada, new HttpRequestException("O planejamento não respondeu."), Cancelamento);
        await transacao.CommitAsync(Cancelamento);

        var mensagem = Assert.Single(await CaixaNoBanco.Todas(banco));
        Assert.False(desistiu);
        Assert.Equal(
            (id, 1, Agora + TimeSpan.FromSeconds(30), (DateTimeOffset?)null, "HttpRequestException: O planejamento não respondeu."),
            (mensagem.Id, mensagem.Tentativas, mensagem.ProximaTentativaEm, mensagem.AbandonadaEm, mensagem.UltimoErro));
    }

    [Fact]
    public async Task Na_decima_falha_a_fila_desiste()
    {
        var id = await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora);
        await CaixaNoBanco.Executar(banco, "update crm.caixa_de_saida set tentativas = 9 where id = @p0", id);
        var (contexto, fila, _) = NovaFila();
        await using var _ = contexto;
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);
        var reservada = (await fila.ReservarProxima(Cancelamento))!;

        Assert.True(await fila.RegistrarFalha(reservada, new InvalidOperationException("De novo."), Cancelamento));
        await transacao.CommitAsync(Cancelamento);

        var abandonada = Assert.Single(await CaixaNoBanco.Todas(banco));
        Assert.Equal((10, (DateTimeOffset?)Agora), (abandonada.Tentativas, abandonada.AbandonadaEm));
    }

    [Fact]
    public async Task A_tratada_e_marcada_com_o_instante_do_tratamento()
    {
        await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora - TimeSpan.FromMinutes(5));
        var (contexto, fila, _) = NovaFila();
        await using var _ = contexto;
        await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);
        var reservada = (await fila.ReservarProxima(Cancelamento))!;

        await fila.MarcarTratada(reservada.Id, Cancelamento);
        await transacao.CommitAsync(Cancelamento);

        Assert.Equal(Agora, Assert.Single(await CaixaNoBanco.Todas(banco)).TratadaEm);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(7, 1_920)]
    [InlineData(8, 3_600)]
    [InlineData(40, 3_600)]
    public void A_espera_dobra_a_cada_falha_ate_uma_hora(int tentativas, int segundos) =>
        Assert.Equal(TimeSpan.FromSeconds(segundos), FilaDaCaixaDeSaida.Espera(tentativas));

    [Fact]
    public async Task A_mensagem_confirmada_avisa_quem_escuta_e_a_desfeita_nao()
    {
        await using var escuta = new NpgsqlConnection(banco.ConexaoDoProcessador);
        await escuta.OpenAsync(Cancelamento);
        await using (var listen = new NpgsqlCommand("listen crm_caixa_de_saida", escuta))
        {
            await listen.ExecuteNonQueryAsync(Cancelamento);
        }

        await using (var desfeita = new NpgsqlConnection(banco.Conexao))
        {
            await desfeita.OpenAsync(Cancelamento);
            await using var transacao = await desfeita.BeginTransactionAsync(Cancelamento);
            await using var insercao = new NpgsqlCommand(
                "insert into crm.caixa_de_saida (id, construtora_id, tipo, formato, conteudo, agregado_id, versao_do_agregado, ocorrida_em, tentativas, proxima_tentativa_em) values (gen_random_uuid(), gen_random_uuid(), 'venda-ganha', 1, '{}', gen_random_uuid(), 1, now(), 0, now())",
                desfeita,
                transacao);
            await insercao.ExecuteNonQueryAsync(Cancelamento);
            await transacao.RollbackAsync(Cancelamento);
        }

        Assert.False(await escuta.WaitAsync(TimeSpan.FromMilliseconds(300), Cancelamento));

        await CaixaNoBanco.GravarCrua(banco, _construtora, CatalogoDeMensagens.Tipos.VendaGanha, Agora);

        Assert.True(await escuta.WaitAsync(TimeSpan.FromSeconds(10), Cancelamento));
    }

    [Fact]
    public async Task A_limpeza_apaga_as_tratadas_antes_da_data_e_deixa_as_pendentes_e_as_abandonadas()
    {
        var tipo = CatalogoDeMensagens.Tipos.VendaGanha;
        var haUmMes = Agora - TimeSpan.FromDays(30);
        var tratadaHaOito = await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, haUmMes, tratadaEm: Agora - TimeSpan.FromDays(8));
        var tratadaHaSeis = await CaixaNoBanco.GravarCrua(banco, Guid.CreateVersion7(), tipo, haUmMes, tratadaEm: Agora - TimeSpan.FromDays(6));
        var pendente = await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, haUmMes);
        var abandonada = await CaixaNoBanco.GravarCrua(banco, _construtora, tipo, haUmMes, abandonadaEm: haUmMes);
        await using var contexto = ContextoDoBanco.Criar(banco.ConexaoDoProcessador, new ConstrutoraAssumida());

        var apagadas = await new ManutencaoDoBanco(contexto).ApagarMensagensTratadasAntesDe(Agora - TimeSpan.FromDays(7), Cancelamento);

        Assert.Equal(1, apagadas);
        Assert.DoesNotContain(tratadaHaOito, (await CaixaNoBanco.Todas(banco)).Select(mensagem => mensagem.Id));
        Assert.Equal(new[] { tratadaHaSeis, pendente, abandonada }.Order(), (await CaixaNoBanco.Todas(banco)).Select(mensagem => mensagem.Id).Order());
    }

    [Fact]
    public async Task A_limpeza_apaga_as_chaves_de_idempotencia_de_toda_construtora_criadas_antes_da_data()
    {
        var sufixo = Guid.CreateVersion7().ToString();
        var limite = Agora - TimeSpan.FromHours(24);

        foreach (var (chave, criadaEm) in new[] { ($"vencida-{sufixo}", limite - TimeSpan.FromSeconds(1)), ($"no-limite-{sufixo}", limite), ($"de-hoje-{sufixo}", Agora) })
        {
            await CaixaNoBanco.Executar(
                banco,
                "insert into crm.chaves_de_idempotencia (construtora_id, chave, impressao_do_pedido, criada_em) values (@p0, @p1, 'POST /api/contas', @p2)",
                Guid.CreateVersion7(),
                chave,
                criadaEm.ToUniversalTime());
        }

        await using var contexto = ContextoDoBanco.Criar(banco.ConexaoDoProcessador, new ConstrutoraAssumida());

        Assert.Equal(1, await new ManutencaoDoBanco(contexto).ApagarChavesDeIdempotenciaAntesDe(limite, Cancelamento));
    }

    /// <summary>A construtora que a segurança por linha vê agora, na conexão do contexto.</summary>
    private static Task<string> Parametro(ContextoDoBanco contexto) =>
        contexto.Database
            .SqlQuery<string>($"select coalesce(current_setting('crm.construtora_id', true), '') as \"Value\"")
            .SingleAsync(Cancelamento);
}

/// <summary>Um relógio parado no instante que o teste escolheu.</summary>
internal sealed class RelogioParado(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}
