using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using CrmObras.Api.Http;

namespace CrmObras.Api.Hospedagem;

/// <summary>
/// O que o documento OpenAPI não descobre sozinho: o esquema de
/// autenticação, e os cabeçalhos que os endpoints exigem — <c>If-Match</c>
/// nas alterações, <c>Idempotency-Key</c> nas criações.
/// </summary>
internal sealed class DocumentacaoDaApi : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    private const string Esquema = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info.Title = "CRM de Obras";
        document.Info.Description =
            "Clientes, leads, funis, vendas, atividades e propostas de uma construtora. " +
            "Toda chamada leva um JWT com a construtora de quem pede; os erros vêm em ProblemDetails (RFC 9457).";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token do provedor de identidade, com as reivindicações construtora, role e preferred_username.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Esquema, document)] = [] });

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadados = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadados.OfType<ExigeVersaoLida>().Any())
        {
            Cabecalho(operation, "If-Match", "A versão lida — a ETag da leitura, como \"3\". Sem ela, 428; desatualizada, 412.");
        }

        if (metadados.OfType<ExigeChaveDeIdempotencia>().Any())
        {
            Cabecalho(
                operation,
                Idempotencia.Cabecalho,
                "Um valor único por operação — um UUID —, repetido em cada nova tentativa. A repetição recebe a resposta da primeira vez.");
        }

        return Task.CompletedTask;
    }

    private static void Cabecalho(OpenApiOperation operacao, string nome, string descricao)
    {
        operacao.Parameters ??= [];
        operacao.Parameters.Add(new OpenApiParameter
        {
            Name = nome,
            In = ParameterLocation.Header,
            Required = true,
            Description = descricao,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        });
    }
}
