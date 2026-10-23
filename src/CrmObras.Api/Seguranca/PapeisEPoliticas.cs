namespace CrmObras.Api.Seguranca;

/// <summary>
/// Os papéis de quem usa o CRM, como vêm no token — na reivindicação
/// <c>role</c>. São os cargos do comercial, os mesmos que dizem a alçada de
/// desconto de cada um (ADR-0007).
/// </summary>
public static class Papeis
{
    /// <summary>Atende os leads, cuida das vendas dele, marca atividades, redige e envia propostas.</summary>
    public const string Vendedor = "vendedor";

    /// <summary>Tudo o que o vendedor faz, e mais: configura os funis, aprova desconto, passa a venda a outro vendedor, remove o que foi cadastrado errado.</summary>
    public const string Gerente = "gerente";

    /// <summary>Como o gerente, com a maior alçada de desconto.</summary>
    public const string Diretor = "diretor";
}

/// <summary>
/// O que cada pedido exige de quem pede (ADR-0014). Toda política começa por
/// ser da construtora: autenticado, com uma construtora válida no token.
/// </summary>
public static class Politicas
{
    /// <summary>Ler qualquer coisa da construtora. Basta ser dela.</summary>
    public const string Consultar = "consultar";

    /// <summary>Cuidar de leads, contas, vendas, atividades e propostas: vendedor, gerente ou diretor.</summary>
    public const string Vender = "vender";

    /// <summary>Configurar funis, aprovar desconto, passar a venda a outro vendedor e remover o que foi cadastrado errado: gerente ou diretor.</summary>
    public const string Gerir = "gerir";
}

/// <summary>Os nomes das reivindicações que a API lê do token, tal como vêm nele.</summary>
public static class Reivindicacoes
{
    /// <summary>O id da construtora de quem fez o pedido — um Guid (ADR-0011).</summary>
    public const string Construtora = "construtora";

    public const string Papel = "role";

    /// <summary>O login de quem pede: é o responsável pelo que ele cria — <c>carlos.souza</c>.</summary>
    public const string Login = "preferred_username";

    public const string Nome = "name";
}
