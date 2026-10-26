using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Mensagens;

namespace CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;

/// <summary>
/// Uma mensagem da caixa de saída: um evento de domínio publicado, gravado
/// na mesma transação do agregado que o registrou, esperando ser tratado
/// (ADR-0016).
/// </summary>
internal sealed class RegistroDeMensagem
{
    public Guid Id { get; set; }

    /// <summary>A ordem de chegada na caixa — a fila trata primeiro o que chegou primeiro.</summary>
    public long Sequencia { get; set; }

    public Guid ConstrutoraId { get; set; }

    /// <summary>O nome do fato: <c>venda-ganha</c>.</summary>
    public string Tipo { get; set; } = string.Empty;

    public short Formato { get; set; }

    /// <summary>A mensagem em JSON — coluna <c>jsonb</c>.</summary>
    public string Conteudo { get; set; } = string.Empty;

    public Guid AgregadoId { get; set; }

    /// <summary>A versão em que o agregado ficou com o fato.</summary>
    public int VersaoDoAgregado { get; set; }

    public DateTimeOffset OcorridaEm { get; set; }

    public int Tentativas { get; set; }

    public DateTimeOffset ProximaTentativaEm { get; set; }

    public DateTimeOffset? TratadaEm { get; set; }

    /// <summary>Quando a fila desistiu da mensagem, depois de falhar tentativas demais. Fica para alguém olhar.</summary>
    public DateTimeOffset? AbandonadaEm { get; set; }

    public string? UltimoErro { get; set; }

    /// <summary>
    /// O rastro de quem publicou a mensagem, no formato <c>traceparent</c> do
    /// W3C: o tratamento continua dele. Nulo se ninguém estava rastreando.
    /// </summary>
    public string? ContextoDoRastreio { get; set; }

    /// <param name="agora">O instante da gravação, guardado em UTC: é o que o <c>timestamptz</c> do PostgreSQL recebe.</param>
    public static RegistroDeMensagem De(EventoDeDominio evento, Guid construtora, int versaoDoAgregado, DateTimeOffset agora)
    {
        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(evento);
        var emUtc = agora.ToUniversalTime();

        return new RegistroDeMensagem
        {
            Id = Guid.CreateVersion7(emUtc),
            ConstrutoraId = construtora,
            Tipo = tipo,
            Formato = CatalogoDeMensagens.Formato,
            Conteudo = conteudo,
            AgregadoId = evento.Agregado.Valor,
            VersaoDoAgregado = versaoDoAgregado,
            OcorridaEm = emUtc,
            ProximaTentativaEm = emUtc,
        };
    }
}
