using System.Net;
using CrmObras.Api.Recursos.Leads;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Recursos.Propostas;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrmObras.Api.Testes;

/*
 * A proposta pela API: o rascunho, o envio por quem tem alçada para o
 * desconto, a aprovação de quem tem mais, a versão nova que substitui a
 * enviada — e o aceite, que ganha a venda junto, ou nada (ADR-0007).
 */
public sealed class PropostasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static readonly object[] Itens =
    [
        new { descricao = "Piso industrial em concreto armado, fck 30 MPa, 15 cm", quantidade = 1_200m, unidade = "m²", precoUnitario = 185.50m },
        new { descricao = "Estrutura metálica do galpão", quantidade = 1m, unidade = "vb", precoUnitario = 1_450_000m },
        new { descricao = "Cobertura em telha termoacústica", quantidade = 1_350m, unidade = "m²", precoUnitario = 312.40m },
    ];

    private readonly CenarioDeVenda _cenario = new(api);

    private static DateTimeOffset Em(int dia, int mes, int hora) => CenarioDeVenda.Em(dia, mes, hora);

    private static object Conteudo(decimal desconto = 0m, string validaAte = "2026-12-31") =>
        new { itens = Itens, desconto, validaAte, condicoesDePagamento = "30% na assinatura e o resto por medição" };

    /// <summary>Uma venda aberta e a proposta dela, em rascunho, na versão 1.</summary>
    private async Task<(Guid Venda, string Proposta)> Rascunho(object? conteudo = null)
    {
        _cenario.Relogio(Em(5, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;
        var proposta = await _cenario.Carlos.Criar("/api/propostas", new { oportunidade = venda, versao = conteudo ?? Conteudo() }).ComSucesso();

        return (venda, proposta.Headers.Location!.ToString());
    }

    [Fact]
    public async Task O_rascunho_mostra_os_valores_como_o_cliente_os_le()
    {
        var (venda, proposta) = await Rascunho();

        var lida = await _cenario.Leitor.LerComSucesso<PropostaResposta>(proposta);

        Assert.Equal((venda, (int?)null, false), (lida.Oportunidade, lida.Vigente, lida.Vencida));
        var versao = Assert.Single(lida.Versoes);
        Assert.Equal((1, SituacaoDaVersao.Rascunho), (versao.Numero, versao.Situacao));
        Assert.Equal([222_600.00m, 1_450_000.00m, 421_740.00m], versao.Itens.Select(item => item.Total));
        Assert.Equal((2_094_340.00m, 0m, 2_094_340.00m), (versao.Subtotal, versao.ValorDoDesconto, versao.Total));
    }

    [Fact]
    public async Task Enviada_em_nome_de_quem_pede_e_com_o_cliente()
    {
        var (_, proposta) = await Rascunho();

        var enviada = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).LerComSucesso<PropostaResposta>();
        var redigir = await _cenario.Carlos.Enviar(HttpMethod.Put, $"{proposta}/rascunho", Conteudo(desconto: 2m), versao: 2);

        Assert.Equal(1, enviada.Vigente);
        Assert.Equal(
            (SituacaoDaVersao.Enviada, new ComercialResposta("carlos.souza", Cargo.Vendedor), Em(5, 11, 9)),
            (enviada.Versoes[0].Situacao, enviada.Versoes[0].EnviadaPor, enviada.Versoes[0].EnviadaEm!.Value));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, redigir.StatusCode);
    }

    [Fact]
    public async Task O_desconto_acima_da_alcada_precisa_de_quem_tenha_alcada_para_ele()
    {
        var (_, proposta) = await Rascunho(Conteudo(desconto: 12m));

        var peloVendedor = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1);
        var aprovacaoDoVendedor = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/aprovacao", versao: 1);
        var aprovacaoDaGerente = await _cenario.Helena.Enviar(HttpMethod.Post, $"{proposta}/aprovacao", versao: 1);
        var aprovada = await _cenario.Roberto.Enviar(HttpMethod.Post, $"{proposta}/aprovacao", versao: 1).LerComSucesso<PropostaResposta>();
        var enviada = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 2).LerComSucesso<PropostaResposta>();

        Assert.Equal("O desconto de 12% passa da alçada do vendedor, de 5%: precisa da aprovação do diretor.", (await peloVendedor.Problema()).Detail);
        Assert.Equal(HttpStatusCode.Forbidden, aprovacaoDoVendedor.StatusCode);
        Assert.Equal("O gerente aprova até 10%: o desconto de 12% é do diretor.", (await aprovacaoDaGerente.Problema()).Detail);
        Assert.Equal(new ComercialResposta("roberto.alves", Cargo.Diretor), aprovada.Versoes[0].Aprovacao?.Aprovador);
        Assert.Equal((251_320.80m, 1_843_019.20m), (enviada.Versoes[0].ValorDoDesconto, enviada.Versoes[0].Total));
    }

    [Fact]
    public async Task Quem_tem_alcada_envia_sem_aprovacao()
    {
        var (_, proposta) = await Rascunho(Conteudo(desconto: 10m));

        var enviada = await _cenario.Helena.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).LerComSucesso<PropostaResposta>();

        Assert.Equal(new ComercialResposta("helena.martins", Cargo.Gerente), enviada.Versoes[0].EnviadaPor);
        Assert.Null(enviada.Versoes[0].Aprovacao);
    }

    [Fact]
    public async Task As_alcadas_e_o_fuso_vem_da_configuracao()
    {
        var alcadas = api.Servicos.GetRequiredService<Alcadas>();
        _cenario.Relogio(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));

        var lead = await _cenario.Carlos.Criar("/api/leads", Exemplos.LeadDaMarina()).LerComSucesso<LeadResposta>();

        Assert.Equal((5m, 10m, 15m), (alcadas.Vendedor, alcadas.Gerente, alcadas.Diretor));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3)), lead.ChegouEm);
        Assert.Equal(TimeSpan.FromHours(-3), lead.ChegouEm.Offset);
    }

    [Fact]
    public async Task A_versao_nova_nasce_igual_e_substitui_a_enviada_quando_vai()
    {
        var (_, proposta) = await Rascunho();
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();

        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/versoes", versao: 2).ComSucesso();
        await _cenario.Carlos.Enviar(HttpMethod.Put, $"{proposta}/rascunho", Conteudo(desconto: 4m), versao: 3).ComSucesso();
        var segunda = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 4).LerComSucesso<PropostaResposta>();

        Assert.Equal(
            [(1, SituacaoDaVersao.Substituida, 2_094_340.00m), (2, SituacaoDaVersao.Enviada, 2_010_566.40m)],
            segunda.Versoes.Select(versao => (versao.Numero, versao.Situacao, versao.Total)));
        Assert.Equal(2, segunda.Vigente);
    }

    [Fact]
    public async Task O_aceite_ganha_a_venda_pelo_total_da_versao_aceita()
    {
        var (venda, proposta) = await Rascunho();
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();
        _cenario.Relogio(Em(7, 12, 11));

        var aceite = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/aceite", versao: 2).ComSucesso();
        var resposta = await aceite.Ler<AceiteResposta>();
        var lida = await _cenario.Leitor.LerComSucesso<OportunidadeResposta>($"/api/oportunidades/{venda}");

        Assert.Equal("\"3\"", aceite.Headers.ETag?.Tag);
        Assert.Equal((1, SituacaoDaVersao.Aceita), (resposta.Proposta.Aceita, resposta.Proposta.Versoes[0].Situacao));
        Assert.Equal((SituacaoDaOportunidade.Ganha, 2_094_340.00m, 2), (resposta.Oportunidade.Situacao, resposta.Oportunidade.ValorFechado, resposta.VersaoDaOportunidade));
        Assert.Equal((SituacaoDaOportunidade.Ganha, Em(7, 12, 11)), (lida.Situacao, lida.FechadaEm!.Value));
    }

    [Fact]
    public async Task Se_a_venda_nao_grava_a_proposta_tambem_nao_fica_aceita()
    {
        // Um gatilho, só para esta venda, recusa a gravação dela: a proposta,
        // gravada antes na mesma transação, volta a estar com o cliente.
        var (venda, proposta) = await Rascunho();
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();
        var gatilho = $"recusa_{venda:N}";

        await Executar(
            $"""
            create function crm.{gatilho}() returns trigger language plpgsql as $$ begin raise exception 'a venda não grava'; end $$;
            create trigger {gatilho} before update on crm.oportunidades for each row when (old.id = '{venda}') execute function crm.{gatilho}();
            """);

        try
        {
            var aceite = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/aceite", versao: 2);

            Assert.Equal(HttpStatusCode.InternalServerError, aceite.StatusCode);
        }
        finally
        {
            await Executar($"drop trigger {gatilho} on crm.oportunidades; drop function crm.{gatilho}();");
        }

        var lida = await _cenario.Leitor.GetAsync(proposta, Cancelamento);
        Assert.Equal((2, SituacaoDaVersao.Enviada), (lida.Versao(), (await lida.Ler<PropostaResposta>()).Versoes[0].Situacao));
        Assert.Equal(SituacaoDaOportunidade.Aberta, (await _cenario.Leitor.LerComSucesso<OportunidadeResposta>($"/api/oportunidades/{venda}")).Situacao);
    }

    [Fact]
    public async Task Vencida_nao_se_aceita_e_recusada_diz_por_que()
    {
        var (_, proposta) = await Rascunho(Conteudo(validaAte: "2026-11-30"));
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();
        _cenario.Relogio(Em(1, 12, 9));

        var lida = await _cenario.Leitor.LerComSucesso<PropostaResposta>(proposta);
        var aceite = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/aceite", versao: 2);
        var recusada = await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/recusa", new { motivo = "Fechou com a Construtora Beta" }, versao: 2)
            .LerComSucesso<PropostaResposta>();

        Assert.True(lida.Vencida);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, aceite.StatusCode);
        Assert.Equal((SituacaoDaVersao.Recusada, "Fechou com a Construtora Beta"), (recusada.Versoes[0].Situacao, recusada.Versoes[0].MotivoDaRecusa));
    }

    [Fact]
    public async Task Lista_as_propostas_da_venda_e_a_gerencia_remove()
    {
        var (venda, proposta) = await Rascunho();
        var id = Guid.Parse(proposta.Split('/')[^1]);

        var propostas = await _cenario.Leitor.LerComSucesso<List<ResumoDaPropostaResposta>>($"/api/propostas?oportunidade={venda}");
        var peloVendedor = await _cenario.Carlos.Enviar(HttpMethod.Delete, proposta, versao: 1);
        var pelaGerente = await _cenario.Helena.Enviar(HttpMethod.Delete, proposta, versao: 1);

        Assert.Equal([(id, 1, SituacaoDaVersao.Rascunho, 2_094_340.00m, 1)], propostas.Select(resumo => (resumo.Id, resumo.Versoes, resumo.Situacao, resumo.Total, resumo.Versao)));
        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.NoContent), (peloVendedor.StatusCode, pelaGerente.StatusCode));
    }

    private async Task Executar(string sql)
    {
        await using var conexao = new NpgsqlConnection(api.Banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync(Cancelamento);
    }
}
