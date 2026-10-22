namespace CrmObras.Infraestrutura.Persistencia;

/*
 * O que pode dar errado ao gravar e ler, cada um com o seu tipo. Quem chama
 * — a API, na Parte 6 — decide o que dizer a quem pediu: 409, 404, 412. Uma
 * DbUpdateException genérica obrigaria a abrir a exceção interna e conferir
 * o código do PostgreSQL em cada lugar.
 */

/// <summary>Alguém gravou o agregado depois que ele foi lido: a versão no banco já não é a esperada.</summary>
public sealed class ConflitoDeConcorrencia : Exception
{
    public ConflitoDeConcorrencia()
    {
    }

    public ConflitoDeConcorrencia(string message)
        : base(message)
    {
    }

    public ConflitoDeConcorrencia(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConflitoDeConcorrencia(string message, int versaoAtual)
        : base(message) => VersaoAtual = versaoAtual;

    /// <summary>A versão em que o agregado está no banco — a que quem gravou precisa ler de novo.</summary>
    public int? VersaoAtual { get; }
}

/// <summary>Não há agregado gravado com o id pedido — ou ele é de outra construtora, o que dá no mesmo.</summary>
public sealed class AgregadoNaoEncontrado : Exception
{
    public AgregadoNaoEncontrado()
    {
    }

    public AgregadoNaoEncontrado(string message)
        : base(message)
    {
    }

    public AgregadoNaoEncontrado(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Já há um agregado gravado com o id de um agregado novo — ou com o que o identifica, como o CNPJ da conta.</summary>
public sealed class AgregadoJaExiste : Exception
{
    public AgregadoJaExiste()
    {
    }

    public AgregadoJaExiste(string message)
        : base(message)
    {
    }

    public AgregadoJaExiste(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Outro agregado aponta para este, que por isso não pode sair do banco.</summary>
public sealed class AgregadoEmUso : Exception
{
    public AgregadoEmUso()
    {
    }

    public AgregadoEmUso(string message)
        : base(message)
    {
    }

    public AgregadoEmUso(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O documento gravado não se lê: JSON fora do formato, formato de outra
/// versão, ou um estado que o domínio recusa.
/// </summary>
public sealed class DocumentoInvalido : Exception
{
    public DocumentoInvalido()
    {
    }

    public DocumentoInvalido(string message)
        : base(message)
    {
    }

    public DocumentoInvalido(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
