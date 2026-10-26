using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrmObras.Observabilidade;

/// <summary>
/// As verificações de saúde que o orquestrador consulta (ADR-0018). Viva: o
/// processo responde, e o que tem de girar está girando — se não, reiniciar
/// resolve. Pronta: viva, e alcançando o banco — se não, o serviço só deixa
/// de receber trabalho até voltar, porque reiniciar não põe o banco no ar.
/// </summary>
public static class Saude
{
    /// <summary>A marca das verificações de que o processo está vivo.</summary>
    public const string Viva = "viva";

    /// <summary>A marca das verificações de que o serviço está pronto para trabalhar.</summary>
    public const string Pronta = "pronta";

    private const string Caminho = "/saude";

    // As letras do português como são, sem os acentos escapados em código
    // Unicode; o que tem sentido em HTML continua escapado.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>O banco responde: uma conexão aberta e um <c>select 1</c>, em até cinco segundos.</summary>
    public static IHealthChecksBuilder VerificarBanco(this IHealthChecksBuilder verificacoes, string conexao)
    {
        ArgumentNullException.ThrowIfNull(verificacoes);
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);

        return verificacoes.Add(new HealthCheckRegistration(
            "banco",
            _ => new BancoNoAr(conexao),
            HealthStatus.Unhealthy,
            [Pronta],
            TimeSpan.FromSeconds(5)));
    }

    /// <summary>
    /// <c>/saude/viva</c> e <c>/saude/pronta</c>, sem token: quem pergunta é
    /// o orquestrador. A resposta diz a situação de cada verificação, e nada
    /// do erro por trás dela. Ficam fora dos rastros, para a pergunta de cada
    /// poucos segundos não encher o painel — e fora do documento OpenAPI, onde
    /// o ASP.NET Core não põe verificação de saúde: não são o contrato da API.
    /// </summary>
    public static IEndpointRouteBuilder MapearSaude(this IEndpointRouteBuilder rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        var saude = rotas.MapGroup(Caminho).AllowAnonymous();
        saude.MapHealthChecks("/viva", Opcoes(Viva));
        saude.MapHealthChecks("/pronta", Opcoes(Viva, Pronta));

        return rotas;
    }

    internal static bool EhVerificacao(PathString caminho) =>
        caminho.StartsWithSegments(Caminho, StringComparison.OrdinalIgnoreCase);

    private static HealthCheckOptions Opcoes(params string[] marcas) => new()
    {
        Predicate = verificacao => verificacao.Tags.Overlaps(marcas),
        ResponseWriter = Escrever,
    };

    private static Task Escrever(HttpContext contexto, HealthReport relatorio)
    {
        var resposta = new RespostaDeSaude(
            relatorio.Status.ToString(),
            relatorio.Entries.ToDictionary(
                entrada => entrada.Key,
                entrada => new SituacaoDaVerificacao(entrada.Value.Status.ToString(), entrada.Value.Description)));

        contexto.Response.ContentType = "application/json; charset=utf-8";

        return JsonSerializer.SerializeAsync(contexto.Response.Body, resposta, Json, contexto.RequestAborted);
    }

    private sealed record RespostaDeSaude(string Situacao, IReadOnlyDictionary<string, SituacaoDaVerificacao> Verificacoes);

    private sealed record SituacaoDaVerificacao(string Situacao, string? Descricao);
}
