namespace CrmObras.Api.Testes;

/// <summary>
/// Os corpos de pedido que os testes usam, como um cliente da API os
/// montaria: a Rota Sul, uma transportadora de Campinas que procura a
/// construtora para erguer um galpão.
/// </summary>
internal static class Exemplos
{
    /// <summary>O CNPJ da matriz da Rota Sul. A filial de Campinas é 11.222.333/0002-62: a mesma raiz.</summary>
    public const string CnpjDaMatriz = "11.222.333/0001-81";

    public const string CnpjDaFilial = "11.222.333/0002-62";

    /// <summary>A matriz da Rota Sul, com o Otávio, que decide, de contato.</summary>
    public static object ContaDaRotaSul(string documento = CnpjDaMatriz) => new
    {
        nome = "Logística Rota Sul Ltda.",
        documento,
        contatos = new[]
        {
            new
            {
                nome = "Otávio Reis",
                email = "otavio.reis@rotasul.com.br",
                telefone = "(19) 3333-4444",
                cargo = "Diretor de operações",
                papel = "Decisor",
            },
        },
    };

    /// <summary>A Marina, da filial de Campinas, que pediu orçamento pelo site.</summary>
    public static object LeadDaMarina(string? documento = CnpjDaFilial) => new
    {
        nome = "Marina Costa",
        origem = "Site",
        empresa = "Logística Rota Sul Ltda.",
        documento,
        email = "marina.costa@rotasul.com.br",
        telefone = "(19) 99876-5432",
        interesse = "Galpão de 1.200 m² em Campinas",
    };

    /// <summary>Necessidade e orçamento confirmados, e quem decide na conversa: qualifica.</summary>
    public static readonly object AvaliacaoQueQualifica = new
    {
        orcamento = "Sim",
        falaComQuemDecide = "Sim",
        necessidade = "Sim",
        prazo = "NaoSabido",
    };

    /// <summary>Um funil de obra pública: da licitação ao contrato.</summary>
    public static readonly object FunilDeObraPublica = new
    {
        nome = "Obras públicas",
        etapas = new object[]
        {
            new { nome = "Edital publicado", probabilidade = 5 },
            new { nome = "Proposta entregue", probabilidade = 30, diasEsperados = 45, requisitos = new[] { "ValorEstimado" } },
            new { nome = "Habilitação", probabilidade = 60, requisitos = new[] { "PrevisaoDeFechamento", "ValorEstimado" } },
        },
    };
}
