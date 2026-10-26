using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;

namespace CrmObras.Processamento;

/// <summary>A fonte dos rastros e o medidor do processamento em segundo plano (ADR-0018).</summary>
public static class TelemetriaDoProcessamento
{
    public const string Nome = "CrmObras.Processamento";

    internal static readonly ActivitySource Fonte = new(Nome);
}

/// <summary>
/// O que a fila mede de cada tentativa de tratar uma mensagem: quantas, de
/// que tipo e com que resultado; quanto a mensagem esperou na caixa; e
/// quanto o tratamento levou.
/// </summary>
/// <remarks>
/// A espera é o número que importa para quem usa: da gravação do fato ao fim
/// do tratamento — a venda ganha chegando ao planejamento como obra. É ela que
/// diz se a consistência eventual está curta como deveria (ADR-0017). Os
/// atributos são só o tipo e o resultado: id de mensagem ou de construtora
/// numa métrica multiplicaria as séries sem fim.
/// </remarks>
public sealed class MetricasDaCaixaDeSaida
{
    public const string Mensagens = "crm.caixa_de_saida.mensagens";

    public const string Espera = "crm.caixa_de_saida.espera";

    public const string Duracao = "crm.caixa_de_saida.duracao";

    private readonly Counter<long> _mensagens;

    private readonly Histogram<double> _espera;

    private readonly Histogram<double> _duracao;

    public MetricasDaCaixaDeSaida(IMeterFactory medidores)
    {
        ArgumentNullException.ThrowIfNull(medidores);

        var medidor = medidores.Create(TelemetriaDoProcessamento.Nome);

        _mensagens = medidor.CreateCounter<long>(
            Mensagens,
            unit: "{mensagem}",
            description: "Tentativas de tratar uma mensagem da caixa de saída, por tipo e resultado: tratada, falhou ou abandonada.");

        // Do aviso de mensagem nova, em milissegundos, às esperas de uma
        // mensagem que falhou, de até uma hora.
        _espera = medidor.CreateHistogram(
            Espera,
            unit: "s",
            description: "Quanto a mensagem esperou: da gravação do fato ao fim desta tentativa de tratá-lo.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 300, 900, 3600] });

        _duracao = medidor.CreateHistogram(
            Duracao,
            unit: "s",
            description: "Quanto levou a tentativa de tratar a mensagem.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10] });
    }

    internal void Registrar(MensagemReservada mensagem, string resultado, TimeSpan espera, TimeSpan duracao)
    {
        var atributos = new TagList
        {
            { "crm.mensagem.tipo", mensagem.Tipo },
            { "crm.mensagem.resultado", resultado },
        };

        _mensagens.Add(1, atributos);
        _espera.Record(Math.Max(espera.TotalSeconds, 0), atributos);
        _duracao.Record(duracao.TotalSeconds, atributos);
    }
}
