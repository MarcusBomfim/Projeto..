using System.Net;
using System.Text.Json;
using Npgsql;

namespace CrmObras.Api.Testes;

/*
 * Os fatos que a API publica (ADR-0016): a venda ganha — direto, ou pelo
 * aceite da proposta — entra na caixa de saída no mesmo pedido, em nome da
 * construtora de quem pediu. O pedido recusado não publica nada.
 */
public sealed class FatosPublicadosTests(ApiDeTestes api)
{
    private readonly CenarioDeVenda _cenario = new(api);

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_ganho_publica_a_venda_ganha_com_a_obra_e_o_valor()
    {
        _cenario.Relogio(CenarioDeVenda.Em(9, 11, 10));
        var (filial, _, _) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;

        await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/ganho", new { valorFechado = 1_843_019.20m }, versao: 1).ComSucesso();

        var (construtora, tipo, versao, conteudo) = Assert.Single(await Publicadas(venda));
        Assert.Equal((_cenario.Construtora, "venda-ganha", 2), (construtora, tipo, versao));
        Assert.Equal(
            (venda, filial, "Galpão de 1.200 m² em Jundiaí", 1_843_019.20m, CenarioDeVenda.Em(9, 11, 10)),
            (conteudo.GetProperty("oportunidade").GetGuid(),
             conteudo.GetProperty("conta").GetGuid(),
             conteudo.GetProperty("titulo").GetString(),
             conteudo.GetProperty("valorFechado").GetDecimal(),
             conteudo.GetProperty("ganhaEm").GetDateTimeOffset()));
    }

    [Fact]
    public async Task O_aceite_da_proposta_publica_a_venda_ganha_pelo_total_dela()
    {
        _cenario.Relogio(CenarioDeVenda.Em(5, 11, 9));
        var (filial, _, _) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;
        var itens = new[] { new { descricao = "Estrutura metálica do galpão", quantidade = 1m, unidade = "vb", precoUnitario = 1_450_000m } };
        var proposta = (await _cenario.Carlos.Criar(
                "/api/propostas",
                new { oportunidade = venda, versao = new { itens, desconto = 0m, validaAte = "2026-12-31", condicoesDePagamento = "À vista" } })
            .ComSucesso()).Headers.Location!.ToString();
        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();

        await _cenario.Carlos.Enviar(HttpMethod.Post, $"{proposta}/aceite", versao: 2).ComSucesso();

        Assert.Equal(1_450_000m, Assert.Single(await Publicadas(venda)).Conteudo.GetProperty("valorFechado").GetDecimal());
    }

    [Fact]
    public async Task O_pedido_recusado_nao_publica_nada()
    {
        var (filial, _, _) = await _cenario.Filial();
        var venda = (await _cenario.Venda(filial, await _cenario.FunilPadrao())).Id;

        var semValor = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/ganho", new { valorFechado = 0m }, versao: 1);
        var versaoVelha = await _cenario.Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/ganho", new { valorFechado = 1_000m }, versao: 7);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, HttpStatusCode.PreconditionFailed), (semValor.StatusCode, versaoVelha.StatusCode));
        Assert.Empty(await Publicadas(venda));
    }

    /// <summary>As mensagens da venda na caixa de saída, vistas pelo dono do banco.</summary>
    private async Task<List<(Guid Construtora, string Tipo, int Versao, JsonElement Conteudo)>> Publicadas(Guid venda)
    {
        await using var conexao = new NpgsqlConnection(api.Banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(
            "select construtora_id, tipo, versao_do_agregado, conteudo::text from crm.caixa_de_saida where agregado_id = @venda order by sequencia",
            conexao);
        comando.Parameters.AddWithValue("venda", venda);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var publicadas = new List<(Guid, string, int, JsonElement)>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            using var documento = JsonDocument.Parse(leitor.GetString(3));
            publicadas.Add((leitor.GetGuid(0), leitor.GetString(1), leitor.GetInt32(2), documento.RootElement.Clone()));
        }

        return publicadas;
    }
}
