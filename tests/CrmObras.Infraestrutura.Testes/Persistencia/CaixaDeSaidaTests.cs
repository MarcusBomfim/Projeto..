using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Infraestrutura.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * A publicação dos fatos (ADR-0016): a venda ganha e a mensagem dela entram
 * no banco na mesma transação — ou nenhuma das duas —, e a mensagem é da
 * construtora da venda, como o resto: outra construtora não a vê, e a API
 * não alcança as funções que atravessam construtoras.
 */
public sealed class CaixaDeSaidaTests(BancoDeTestes banco)
{
    private static readonly DateTimeOffset Ganho = Carteira.Segunda.AddDays(20);

    private readonly Carteira _carteira = new(banco, Guid.CreateVersion7());

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private Task<List<MensagemNoBanco>> MensagensDa(Oportunidade venda) => CaixaNoBanco.DoAgregado(banco, venda.Id.Valor);

    [Fact]
    public async Task A_venda_ganha_e_publicada_na_gravacao_dela()
    {
        var (conta, funil, venda) = await _carteira.UmaVendaGravada();
        var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        lida.Agregado.Ganhar(funil, Dinheiro.Reais(2_350_000m), Ganho);

        var versao = await _carteira.Oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento);

        var mensagem = Assert.Single(await MensagensDa(venda));
        Assert.Equal(
            (_carteira.Construtora, CatalogoDeMensagens.Tipos.VendaGanha, versao, 0, (DateTimeOffset?)null, (DateTimeOffset?)null),
            (mensagem.Construtora, mensagem.Tipo, mensagem.VersaoDoAgregado, mensagem.Tentativas, mensagem.TratadaEm, mensagem.AbandonadaEm));
        Assert.Equal(
            new MensagemDeVendaGanha(venda.Id.Valor, conta.Id.Valor, "Galpão de 1.200 m² em Jundiaí", 2_350_000m, Ganho),
            CatalogoDeMensagens.Ler(mensagem.Tipo, mensagem.Formato, mensagem.Conteudo));

        // Publicado, o fato sai do agregado: gravar de novo não o publica outra vez.
        Assert.Empty(lida.Agregado.Eventos);
        lida.Agregado.Renomear("Galpão logístico de Jundiaí");
        await _carteira.Oportunidades.Atualizar(lida.Agregado, versao, Cancelamento);
        Assert.Single(await MensagensDa(venda));
    }

    [Fact]
    public async Task Gravacao_sem_fato_nao_publica_nada()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        lida.Agregado.MoverPara(funil, funil.Chamada("Negociação").Id, Ganho);

        await _carteira.Oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento);

        Assert.Empty(await MensagensDa(venda));
    }

    [Fact]
    public async Task A_venda_ganha_antes_da_primeira_gravacao_e_publicada_com_ela()
    {
        var (conta, funil, _) = await _carteira.UmaVendaGravada();
        var venda = VendasDeTeste.Completa(funil, conta);
        venda.Ganhar(funil, Dinheiro.Reais(980_000m), Ganho);

        await _carteira.Oportunidades.Adicionar(venda, Cancelamento);

        Assert.Equal(1, Assert.Single(await MensagensDa(venda)).VersaoDoAgregado);
    }

    [Fact]
    public async Task No_conflito_de_versao_nem_a_venda_nem_a_mensagem()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var primeira = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        var segunda = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        primeira.Agregado.Renomear("Galpão logístico de Jundiaí");
        await _carteira.Oportunidades.Atualizar(primeira.Agregado, primeira.Versao, Cancelamento);

        segunda.Agregado.Ganhar(funil, Dinheiro.Reais(2_350_000m), Ganho);
        await Assert.ThrowsAsync<ConflitoDeConcorrencia>(() => _carteira.Oportunidades.Atualizar(segunda.Agregado, segunda.Versao, Cancelamento));

        Assert.Empty(await MensagensDa(venda));
        Assert.Equal(SituacaoDaOportunidade.Aberta, (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!.Agregado.Situacao);
    }

    [Fact]
    public async Task Se_a_mensagem_nao_grava_a_venda_tambem_nao_fica_ganha()
    {
        // Um gatilho, só para esta venda, recusa a mensagem dela: a venda,
        // gravada antes na mesma transação, volta a estar aberta.
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var gatilho = $"recusa_{venda.Id.Valor:N}";
        await CaixaNoBanco.Executar(
            banco,
            $"""
            create function crm.{gatilho}() returns trigger language plpgsql as $$ begin raise exception 'a mensagem não grava'; end $$;
            create trigger {gatilho} before insert on crm.caixa_de_saida for each row when (new.agregado_id = '{venda.Id}') execute function crm.{gatilho}();
            """);

        try
        {
            var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
            lida.Agregado.Ganhar(funil, Dinheiro.Reais(2_350_000m), Ganho);

            await Assert.ThrowsAnyAsync<Exception>(() => _carteira.Oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento));
        }
        finally
        {
            await CaixaNoBanco.Executar(banco, $"drop trigger {gatilho} on crm.caixa_de_saida; drop function crm.{gatilho}();");
        }

        var depois = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        Assert.Equal((SituacaoDaOportunidade.Aberta, 1), (depois.Agregado.Situacao, depois.Versao));
    }

    [Fact]
    public async Task Na_transacao_de_quem_chama_a_mensagem_fica_ou_sai_com_ela()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var proposta = await _carteira.UmaPropostaGravada(venda);

        // O aceite como a API o faz: a proposta e a venda pelo mesmo contexto, numa transação.
        async Task Aceitar(bool confirmar)
        {
            await using var contexto = banco.NovoContexto(_carteira.Construtora);
            var propostas = new RepositorioDePropostas(contexto, TimeProvider.System);
            var oportunidades = new RepositorioDeOportunidades(contexto, TimeProvider.System);
            var lidaProposta = (await propostas.Obter(proposta.Id, Cancelamento))!;
            var lidaVenda = (await oportunidades.Obter(venda.Id, Cancelamento))!;
            FechamentoPelaProposta.Aceitar(lidaProposta.Agregado, lidaVenda.Agregado, funil, Ganho);

            await using var transacao = await contexto.Database.BeginTransactionAsync(Cancelamento);
            await propostas.Atualizar(lidaProposta.Agregado, lidaProposta.Versao, Cancelamento);
            await oportunidades.Atualizar(lidaVenda.Agregado, lidaVenda.Versao, Cancelamento);

            if (confirmar)
            {
                await transacao.CommitAsync(Cancelamento);
            }
        }

        await Aceitar(confirmar: false);
        Assert.Empty(await MensagensDa(venda));

        await Aceitar(confirmar: true);
        var ganha = Assert.IsType<MensagemDeVendaGanha>(CatalogoDeMensagens.Ler(CatalogoDeMensagens.Tipos.VendaGanha, 1, Assert.Single(await MensagensDa(venda)).Conteudo));
        Assert.Equal(proposta.Atual.Total.Valor, ganha.ValorFechado);
    }

    [Fact]
    public async Task A_mensagem_e_da_construtora_da_venda_no_codigo_e_no_banco()
    {
        var (_, funil, venda) = await _carteira.UmaVendaGravada();
        var lida = (await _carteira.Oportunidades.Obter(venda.Id, Cancelamento))!;
        lida.Agregado.Ganhar(funil, Dinheiro.Reais(2_350_000m), Ganho);
        await _carteira.Oportunidades.Atualizar(lida.Agregado, lida.Versao, Cancelamento);
        var outra = Guid.CreateVersion7();

        Task<int> Quantas(ContextoDoBanco contexto, bool comFiltro) =>
            (comFiltro ? contexto.CaixaDeSaida : contexto.CaixaDeSaida.IgnoreQueryFilters())
                .CountAsync(mensagem => mensagem.AgregadoId == venda.Id.Valor, Cancelamento);

        Assert.Equal(1, await Quantas(banco.NovoContexto(_carteira.Construtora), comFiltro: true));
        Assert.Equal(0, await Quantas(banco.NovoContexto(outra), comFiltro: true));
        Assert.Equal(0, await Quantas(banco.NovoContexto(outra), comFiltro: false));
        Assert.Equal(0, await Quantas(banco.NovoContextoSemConstrutora(), comFiltro: false));

        // O dono do banco passa por cima da segurança por linha: aí, quem isola é só o filtro do código.
        Assert.Equal(0, await Quantas(ContextoDoBanco.Criar(banco.Conexao, new ConstrutoraFixa(outra)), comFiltro: true));
    }

    [Theory]
    [InlineData("select * from crm.reservar_mensagens(1, now())")]
    [InlineData("select crm.apagar_mensagens_tratadas_antes_de(now())")]
    [InlineData("select crm.apagar_chaves_de_idempotencia_antes_de(now())")]
    public async Task A_aplicacao_nao_alcanca_o_que_atravessa_construtoras(string sql)
    {
        await using var conexao = new NpgsqlConnection(banco.ConexaoDaAplicacao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Cancelamento));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    [Fact]
    public async Task O_banco_recusa_mensagem_que_nao_e_um_objeto_json()
    {
        var erro = await Assert.ThrowsAsync<PostgresException>(() =>
            CaixaNoBanco.GravarCrua(banco, _carteira.Construtora, CatalogoDeMensagens.Tipos.VendaGanha, Ganho, conteudo: "[]"));

        Assert.Equal(("23514", "ck_caixa_de_saida_conteudo"), (erro.SqlState, erro.ConstraintName));
    }
}
