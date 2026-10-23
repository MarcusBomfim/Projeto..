using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrmObras.Api.Testes;

/*
 * O contrato da API — o documento OpenAPI — fica no repositório, em
 * docs/api/openapi.json, e é dele que o front-end gera os seus tipos
 * (ADR-0015). A API mudou e o arquivo não: este teste falha. Para regravar o
 * arquivo, de propósito:
 *
 *     ATUALIZAR_CONTRATO=1 dotnet test --solution CrmObras.slnx
 *
 * e o diff dele vai no mesmo commit, para quem revisa ver o que mudou no
 * contrato — e o front-end regenerar os tipos.
 */
public sealed class ContratoPublicadoTests(ApiDeTestes api)
{
    private static readonly JsonSerializerOptions Escrita = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static string CaminhoDoContrato()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);

        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "CrmObras.slnx")))
        {
            pasta = pasta.Parent;
        }

        Assert.True(pasta is not null, "A raiz do repositório não foi encontrada a partir da pasta dos testes.");

        return Path.Combine(pasta!.FullName, "docs", "api", "openapi.json");
    }

    [Fact]
    public async Task O_contrato_publicado_e_o_que_a_API_serve()
    {
        var resposta = await api.Anonimo().GetAsync("/openapi/v1.json", Cancelamento);
        var documento = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(Cancelamento))!.AsObject();

        // O endereço muda com o lugar onde a API roda; o contrato, não.
        documento.Remove("servers");
        var servido = documento.ToJsonString(Escrita) + "\n";
        var caminho = CaminhoDoContrato();

        if (Environment.GetEnvironmentVariable("ATUALIZAR_CONTRATO") == "1")
        {
            await File.WriteAllTextAsync(caminho, servido, Cancelamento);

            return;
        }

        Assert.True(File.Exists(caminho), $"O contrato não está em {caminho}: rode os testes com ATUALIZAR_CONTRATO=1.");

        var publicado = (await File.ReadAllTextAsync(caminho, Cancelamento)).ReplaceLineEndings("\n");

        if (publicado != servido)
        {
            Assert.Fail(
                $"""
                O contrato da API mudou e docs/api/openapi.json não. A primeira diferença:
                {PrimeiraDiferenca(publicado, servido)}
                Se a mudança foi de propósito, rode os testes com ATUALIZAR_CONTRATO=1 e, em web/, npm run tipos.
                """);
        }
    }

    private static string PrimeiraDiferenca(string publicado, string servido)
    {
        var linhasPublicadas = publicado.Split('\n');
        var linhasServidas = servido.Split('\n');
        var linha = Enumerable.Range(0, Math.Max(linhasPublicadas.Length, linhasServidas.Length))
            .First(indice => indice >= linhasPublicadas.Length || indice >= linhasServidas.Length || linhasPublicadas[indice] != linhasServidas[indice]);

        return $"""
            linha {linha + 1}
              no arquivo: {(linha < linhasPublicadas.Length ? linhasPublicadas[linha].Trim() : "(fim do arquivo)")}
              na API:     {(linha < linhasServidas.Length ? linhasServidas[linha].Trim() : "(fim do documento)")}
            """;
    }
}
