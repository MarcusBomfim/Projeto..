using System.Text.Json.Serialization;
using CrmObras.Api.Http;
using CrmObras.Api.Recursos;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Observabilidade;

namespace CrmObras.Api.Hospedagem;

/// <summary>Os serviços da API: observabilidade, persistência, segurança, erros, JSON, documentação, as alçadas de desconto e o fuso da construtora.</summary>
internal static class ServicosDaApi
{
    public static WebApplicationBuilder AdicionarApi(this WebApplicationBuilder builder)
    {
        var conexao = builder.Configuration.GetConnectionString("Crm")
            ?? throw new InvalidOperationException("Falta a cadeia de conexão ConnectionStrings:Crm.");

        builder.AdicionarObservabilidade("crm-obras-api");
        builder.Services.AddHealthChecks().VerificarBanco(conexao);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AdicionarPersistencia<ConstrutoraDoUsuario>(conexao);
        builder.Services.AdicionarSeguranca();
        builder.Services.AddSingleton(LerAlcadas(builder.Configuration.GetSection("Alcadas")));

        var zona = LerFuso(builder.Configuration["Fuso"] ?? Fuso.Padrao);
        builder.Services.AddSingleton(provedor => new Fuso(zona, provedor.GetRequiredService<TimeProvider>()));

        builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = Problemas.Completar);
        builder.Services.AddExceptionHandler<TradutorDeExcecoes>();

        // JSON estrito, como a leitura dos documentos (ADR-0011): campo
        // obrigatório que falta, nulo onde não cabe, campo que o contrato não
        // tem, enum em número e número em texto são recusados — e não viram
        // zero em silêncio.
        builder.Services.ConfigureHttpJsonOptions(opcoes =>
        {
            var json = opcoes.SerializerOptions;
            json.RespectNullableAnnotations = true;
            json.RespectRequiredConstructorParameters = true;
            json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            json.NumberHandling = JsonNumberHandling.Strict;
            json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        });

        // Pedido malformado lança, e o tradutor de exceções explica o que
        // estava errado. Sem isso, seria um 400 mudo.
        builder.Services.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);

        builder.Services.AddOpenApi(opcoes =>
        {
            opcoes.AddDocumentTransformer<DocumentacaoDaApi>();
            opcoes.AddOperationTransformer<DocumentacaoDaApi>();
        });

        return builder;
    }

    /// <summary>
    /// As alçadas de desconto da instalação, da seção <c>Alcadas</c> — ou as
    /// padrão, sem ela (ADR-0007). Configuração que o domínio recusa impede a
    /// API de subir: melhor do que aprovar desconto pela regra errada.
    /// </summary>
    private static Alcadas LerAlcadas(IConfigurationSection secao)
    {
        return secao.Exists()
            ? new Alcadas(Exigir(secao, nameof(Cargo.Vendedor)), Exigir(secao, nameof(Cargo.Gerente)), Exigir(secao, nameof(Cargo.Diretor)))
            : Alcadas.Padrao;

        static decimal Exigir(IConfigurationSection secao, string cargo) =>
            secao.GetValue<decimal?>(cargo) ?? throw new InvalidOperationException($"Falta a alçada {secao.Path}:{cargo} na configuração.");
    }

    /// <summary>O fuso da construtora, pelo nome da base IANA. Nome que não existe impede a API de subir: o dia de tudo dependeria dele.</summary>
    private static TimeZoneInfo LerFuso(string nome) =>
        Fuso.Ler(nome) ?? throw new InvalidOperationException($"O fuso {nome}, da configuração Fuso, não existe: use um nome da base IANA, como {Fuso.Padrao}.");
}
