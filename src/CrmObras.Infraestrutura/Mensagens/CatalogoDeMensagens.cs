using System.Text.Json;
using System.Text.Json.Serialization;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Infraestrutura.Mensagens;

/*
 * O formato publicado de cada evento de domínio: o contrato com quem processa
 * a caixa de saída (ADR-0016). Separado do evento como os documentos são
 * separados dos agregados (ADR-0011) — o domínio pode mudar sem mudar o que
 * já está na fila, esperando para ser tratado.
 */

/// <summary>A venda ganha, como vai para a fila: a obra, o cliente e o valor de quando foi ganha.</summary>
public sealed record MensagemDeVendaGanha(Guid Oportunidade, Guid Conta, string Titulo, decimal ValorFechado, DateTimeOffset GanhaEm);

/// <summary>A tradução entre os eventos de domínio e as mensagens da caixa de saída, nos dois sentidos.</summary>
public static class CatalogoDeMensagens
{
    /// <summary>O formato em que as mensagens são escritas hoje.</summary>
    public const short Formato = 1;

    /// <summary>O nome de cada mensagem na caixa: é por ele que os tratadores a assinam.</summary>
    public static class Tipos
    {
        public const string VendaGanha = "venda-ganha";
    }

    /// <summary>O tipo da mensagem e o conteúdo dela, em JSON.</summary>
    /// <exception cref="InvalidOperationException">O evento ainda não tem mensagem no catálogo.</exception>
    public static (string Tipo, string Conteudo) Escrever(EventoDeDominio evento) => evento switch
    {
        VendaGanha ganha => (
            Tipos.VendaGanha,
            JsonSerializer.Serialize(
                new MensagemDeVendaGanha(ganha.Oportunidade.Valor, ganha.Conta.Valor, ganha.Titulo, ganha.ValorFechado.Valor, ganha.GanhaEm),
                ContextoJsonDasMensagens.Default.MensagemDeVendaGanha)),
        null => throw new ArgumentNullException(nameof(evento)),
        _ => throw new InvalidOperationException($"O evento {evento.GetType().Name} não tem mensagem no catálogo: acrescente a dele."),
    };

    /// <summary>A mensagem gravada, de volta ao tipo dela.</summary>
    /// <exception cref="InvalidOperationException">Tipo ou formato que esta versão do sistema não conhece.</exception>
    public static object Ler(string tipo, short formato, string conteudo)
    {
        if (formato != Formato)
        {
            throw new InvalidOperationException($"A mensagem {tipo} está no formato {formato}, e esta versão do sistema lê o formato {Formato}.");
        }

        object? mensagem = tipo switch
        {
            Tipos.VendaGanha => JsonSerializer.Deserialize(conteudo, ContextoJsonDasMensagens.Default.MensagemDeVendaGanha),
            _ => throw new InvalidOperationException($"Tipo de mensagem desconhecido: {tipo}."),
        };

        return mensagem ?? throw new InvalidOperationException($"A mensagem {tipo} está vazia.");
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MensagemDeVendaGanha))]
internal sealed partial class ContextoJsonDasMensagens : JsonSerializerContext;
