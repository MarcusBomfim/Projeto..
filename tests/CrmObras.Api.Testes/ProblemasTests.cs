using System.Net;
using System.Text;
using System.Text.Json;
using CrmObras.Api.Seguranca;
using Npgsql;

namespace CrmObras.Api.Testes;

/*
 * Todo erro sai em ProblemDetails (RFC 9457), com um tipo que aponta para a
 * explicação, um título fixo por tipo, o detalhe do que aconteceu e o id do
 * rastreio. Erro do cliente diz o que estava errado; erro interno não vaza
 * nada além do id (ADR-0014).
 */
public sealed class ProblemasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly HttpClient _vendedor = api.Cliente(Guid.CreateVersion7(), Papeis.Vendedor);

    [Fact]
    public async Task Regra_do_negocio_e_422_com_a_mensagem_do_dominio()
    {
        var resposta = await _vendedor.Criar("/api/leads", new { nome = "Marina Costa", origem = "Indicacao" });
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith("#regra-do-negocio", problema.Type, StringComparison.Ordinal);
        Assert.Equal("O pedido fere uma regra do negócio.", problema.Title);
        Assert.Equal(
            "O lead precisa de um e-mail ou de um telefone: sem um dos dois, não há como responder. Lead de indicação diz quem indicou.",
            problema.Detail);
        Assert.Equal("POST /api/leads", problema.Instance);
        Assert.True(problema.Extensions.ContainsKey("traceId"));
    }

    [Theory]
    [InlineData("\"11.222.333/0001-80\"", "dígito")]
    [InlineData("\"123\"", "não é CPF nem CNPJ")]
    public async Task Documento_que_nao_se_le_e_422_dizendo_por_que(string documento, string motivo)
    {
        var resposta = await _vendedor.Criar("/api/contas", $$"""{"nome": "Rota Sul", "documento": {{documento}}}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains(motivo, (await resposta.Problema()).Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{}""", "nome")]
    [InlineData("""{"nome": null}""", "nome")]
    [InlineData("""{"nome": "Rota Sul", "cor": "azul"}""", "cor")]
    [InlineData("""{"nome": "Rota Sul", "contatos": [{"nome": "Otávio", "email": "otavio@rotasul.com.br", "papel": 1}]}""", "papel")]
    [InlineData("""{"nome": "Rota Sul", "contatos": [{"nome": "Otávio", "email": "otavio@rotasul.com.br", "papel": "Chefe"}]}""", "papel")]
    [InlineData("""{"nome": "Rota Sul",""", "")]
    public async Task Corpo_fora_do_contrato_e_400_dizendo_o_que_estava_errado(string json, string campo)
    {
        var resposta = await _vendedor.Criar("/api/contas", json);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.EndsWith("#pedido-invalido", problema.Type, StringComparison.Ordinal);
        Assert.StartsWith("O corpo não é o JSON esperado", problema.Detail, StringComparison.Ordinal);
        Assert.Contains(campo, problema.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Numero_em_texto_tambem_e_400()
    {
        var gerente = api.Cliente(Guid.CreateVersion7(), Papeis.Gerente);

        var resposta = await gerente.Criar("/api/funis", """{"nome": "Obras", "etapas": [{"nome": "Contato", "probabilidade": "10"}]}""");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("probabilidade", (await resposta.Problema()).Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/leads", "situacao")]
    [InlineData("/api/leads?situacao=novo", "Novo, EmQualificacao, Qualificado, Descartado, Convertido")]
    [InlineData("/api/leads?situacao=0", "Novo, EmQualificacao, Qualificado, Descartado, Convertido")]
    public async Task Consulta_fora_do_contrato_e_400(string caminho, string explicacao)
    {
        var resposta = await _vendedor.GetAsync(caminho, Cancelamento);
        var problema = await resposta.Problema();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(explicacao, problema.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corpo_que_nao_e_JSON_e_415()
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/contas")
        {
            Content = new StringContent("nome=Rota Sul", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        pedido.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resposta = await _vendedor.SendAsync(pedido, Cancelamento);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resposta.StatusCode);
        Assert.EndsWith("#tipo-nao-suportado", (await resposta.Problema()).Type, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rota_ou_metodo_que_nao_existe_tambem_vem_em_problema()
    {
        var semRota = await _vendedor.GetAsync("/api/nada", Cancelamento);
        var semMetodo = await _vendedor.PutAsync("/api/contas", new StringContent("{}", Encoding.UTF8, "application/json"), Cancelamento);

        Assert.Equal((HttpStatusCode.NotFound, "Não encontrado."), (semRota.StatusCode, (await semRota.Problema()).Title));
        Assert.Equal(
            (HttpStatusCode.MethodNotAllowed, "Este recurso não aceita este método."),
            (semMetodo.StatusCode, (await semMetodo.Problema()).Title));
    }

    [Fact]
    public async Task Erro_interno_nao_vaza_o_que_houve_so_o_id_do_rastreio()
    {
        // Alguém editou o banco à mão e pôs no documento um campo que o
        // contrato de leitura não tem.
        var id = await _vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul()).IdCriado();

        await using (var conexao = new NpgsqlConnection(api.Banco.Conexao))
        {
            await conexao.OpenAsync(Cancelamento);
            await using var comando = new NpgsqlCommand("""update crm.contas set documento = documento || '{"corDoLogotipo": "azul"}' where id = @id""", conexao);
            comando.Parameters.AddWithValue("id", id);
            await comando.ExecuteNonQueryAsync(Cancelamento);
        }

        var resposta = await _vendedor.GetAsync($"/api/contas/{id}", Cancelamento);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);
        var problema = JsonSerializer.Deserialize<JsonElement>(corpo);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        Assert.Equal("Erro interno.", problema.GetProperty("title").GetString());
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.True(problema.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("corDoLogotipo", corpo, StringComparison.Ordinal);
    }
}
