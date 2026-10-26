using System.Diagnostics;

namespace CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;

/// <summary>
/// O rastro da caixa de saída, dos dois lados: quem publica a mensagem, na
/// transação do agregado, e quem a trata, em segundo plano. O contexto do
/// rastro vai gravado na mensagem, e o tratamento continua o rastro do pedido
/// que registrou o fato (ADR-0016).
/// </summary>
/// <remarks>
/// Os atributos seguem as convenções de mensageria do OpenTelemetry. Não há
/// nome padrão para uma caixa de saída numa tabela do PostgreSQL: o sistema
/// de mensageria é <c>caixa_de_saida</c>. O destino é o tipo da mensagem, e
/// não a tabela: os tratadores assinam por tipo, como se assina um tópico.
/// </remarks>
public static class TelemetriaDaCaixaDeSaida
{
    /// <summary>O nome da fonte dos rastros da persistência.</summary>
    public const string NomeDaFonte = "CrmObras.Infraestrutura";

    public const string Sistema = "caixa_de_saida";

    internal static readonly ActivitySource Fonte = new(NomeDaFonte);

    /// <summary>Os atributos que a publicação e o tratamento de uma mensagem têm em comum.</summary>
    public static TagList Atributos(Guid mensagem, string tipo, Guid construtora, string operacao) => new()
    {
        { "messaging.system", Sistema },
        { "messaging.operation.type", operacao },
        { "messaging.destination.name", tipo },
        { "messaging.message.id", mensagem.ToString() },
        { "crm.mensagem.tipo", tipo },
        { "crm.construtora.id", construtora.ToString() },
    };
}
