using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;

namespace CrmObras.Api.Http;

/// <summary>
/// A versão que o cliente leu, do cabeçalho <c>If-Match</c>. Todo endpoint
/// que altera ou remove um agregado recebe uma — e a alteração só vale se
/// ninguém gravou depois da leitura (ADR-0012, ADR-0014).
/// </summary>
/// <remarks>
/// Sem <c>If-Match</c>, ou com <c>*</c>, o pedido é recusado com 428: gravar
/// sem dizer o que se leu é gravar por cima do que os outros fizeram.
/// </remarks>
public sealed record VersaoLida(int Valor) : IBindableFromHttpContext<VersaoLida>, IEndpointParameterMetadataProvider
{
    public static ValueTask<VersaoLida?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cabecalho = context.Request.Headers.IfMatch;

        if (cabecalho.Count == 0 || cabecalho is ["*"])
        {
            throw new VersaoNaoInformada(
                "Toda alteração diz a versão que leu, no cabeçalho If-Match — a ETag da leitura, como \"3\".");
        }

        if (cabecalho.Count != 1 || !Etiquetas.TentarLer(cabecalho[0], out var versao))
        {
            throw new PedidoInvalido($"O If-Match leva uma versão só, entre aspas, como \"3\"; veio {cabecalho}.");
        }

        return ValueTask.FromResult<VersaoLida?>(new VersaoLida(versao));
    }

    public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(ExigeVersaoLida.Instancia);
    }
}

/// <summary>Marca, nos metadados do endpoint, que ele exige <c>If-Match</c> — para a documentação e os testes.</summary>
public sealed class ExigeVersaoLida
{
    private ExigeVersaoLida()
    {
    }

    public static ExigeVersaoLida Instancia { get; } = new();
}
