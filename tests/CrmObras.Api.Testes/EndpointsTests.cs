using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrmObras.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Api.Testes;

/*
 * As regras que valem para todo endpoint, conferidas percorrendo os
 * endpoints que a API mapeou — os de hoje e os que vierem. Um recurso novo
 * que esqueça a política, o If-Match ou a chave de idempotência quebra aqui,
 * e não em produção (ADR-0014).
 */
public sealed partial class EndpointsTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private IReadOnlyList<RouteEndpoint> DaApi() =>
        [.. api.Servicos.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)];

    private static string Metodo(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Single();

    private static string Descrever(RouteEndpoint endpoint) => $"{Metodo(endpoint)} {endpoint.RoutePattern.RawText}";

    [GeneratedRegex(@"\{(\w+):[^}]+\}")]
    private static partial Regex Restricao();

    /// <summary>Alterar ou remover algo que já existe: o caminho tem o id do agregado, e o método não é de leitura.</summary>
    private static bool AlteraAgregadoExistente(RouteEndpoint endpoint) =>
        Metodo(endpoint) != "GET" && endpoint.RoutePattern.RawText!.Contains("{id", StringComparison.Ordinal);

    /// <summary>Criar um agregado: POST sem id no caminho.</summary>
    private static bool CriaAgregado(RouteEndpoint endpoint) =>
        Metodo(endpoint) == "POST" && !endpoint.RoutePattern.RawText!.Contains('{', StringComparison.Ordinal);

    [Fact]
    public void A_api_tem_endpoints()
    {
        Assert.NotEmpty(DaApi());
    }

    [Fact]
    public void Todo_endpoint_exige_uma_politica_com_nome_e_nenhum_e_anonimo()
    {
        Assert.All(DaApi(), endpoint =>
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), dado => !string.IsNullOrEmpty(dado.Policy));
        });
    }

    [Fact]
    public void Toda_alteracao_de_agregado_exige_a_versao_lida()
    {
        Assert.All(
            DaApi().Where(AlteraAgregadoExistente),
            endpoint => Assert.True(endpoint.Metadata.GetMetadata<ExigeVersaoLida>() is not null, $"{Descrever(endpoint)} não exige If-Match."));
    }

    [Fact]
    public void Toda_criacao_exige_chave_de_idempotencia_e_nada_mais_exige()
    {
        foreach (var endpoint in DaApi())
        {
            var exige = endpoint.Metadata.GetMetadata<ExigeChaveDeIdempotencia>() is not null;

            Assert.True(exige == CriaAgregado(endpoint), $"{Descrever(endpoint)}: Idempotency-Key {(exige ? "sem ser criação" : "faltando")}.");
        }
    }

    [Fact]
    public async Task O_documento_OpenAPI_descreve_os_cabecalhos_exigidos()
    {
        var documento = JsonNode.Parse(await (await api.Anonimo().GetAsync("/openapi/v1.json", Cancelamento)).Content.ReadAsStringAsync(Cancelamento))!;

        foreach (var endpoint in DaApi())
        {
            // No documento, os parâmetros vêm sem a restrição de rota: {id}, e não {id:guid}.
            var caminho = Restricao().Replace(endpoint.RoutePattern.RawText!, "{$1}").TrimEnd('/');
            var operacao = documento["paths"]?[caminho]?[Metodo(endpoint).ToLowerInvariant()];
            var cabecalhos = (operacao?["parameters"]?.AsArray() ?? [])
                .Where(parametro => parametro?["in"]?.GetValue<string>() == "header")
                .Select(parametro => parametro!["name"]!.GetValue<string>())
                .ToList();

            Assert.True(operacao is not null, $"{Descrever(endpoint)} não está no documento OpenAPI.");
            Assert.Equal(endpoint.Metadata.GetMetadata<ExigeVersaoLida>() is not null, cabecalhos.Contains("If-Match"));
            Assert.Equal(endpoint.Metadata.GetMetadata<ExigeChaveDeIdempotencia>() is not null, cabecalhos.Contains("Idempotency-Key"));
        }
    }
}
