namespace CrmObras.Api.Http;

/*
 * O que está errado no pedido em si — e não no que ele pede ao domínio.
 * Viram 400 e 428 na tradução de exceções.
 */

/// <summary>O pedido não está no formato esperado: um cabeçalho malformado, um corpo que não é o JSON combinado.</summary>
public sealed class PedidoInvalido : Exception
{
    public PedidoInvalido()
    {
    }

    public PedidoInvalido(string message)
        : base(message)
    {
    }

    public PedidoInvalido(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A alteração veio sem a versão lida — sem <c>If-Match</c>.</summary>
public sealed class VersaoNaoInformada : Exception
{
    public VersaoNaoInformada()
    {
    }

    public VersaoNaoInformada(string message)
        : base(message)
    {
    }

    public VersaoNaoInformada(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
