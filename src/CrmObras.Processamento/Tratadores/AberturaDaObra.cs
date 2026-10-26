using System.Diagnostics;
using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia;
using CrmObras.Processamento.Planejamento;
using Microsoft.Extensions.Logging;

namespace CrmObras.Processamento.Tratadores;

/// <summary>
/// A venda ganha vira obra: o cronograma dela nasce no planejamento-obras,
/// com o nome da obra, em nome da construtora da venda (ADR-0017).
/// </summary>
/// <remarks>
/// <para>
/// A entrega é "ao menos uma vez": a mesma mensagem pode chegar de novo — o
/// processador caiu depois de o planejamento abrir o cronograma e antes de
/// marcar a mensagem. Por isso o pedido leva uma chave de idempotência feita
/// da venda: repetido, o planejamento devolve o cronograma que já abriu, e a
/// obra não nasce duas vezes. A venda ganha não se reabre, então não há
/// segunda obra legítima para a mesma venda.
/// </para>
/// <para>
/// O nome sai da mensagem, que é o fato do instante do ganho (ADR-0016): cada
/// tentativa manda o mesmo pedido, como a chave de idempotência exige.
/// </para>
/// </remarks>
public sealed partial class AberturaDaObra(
    ClienteDoPlanejamento planejamento,
    IConstrutoraAtual construtora,
    ILogger<AberturaDaObra> registro) : ITratadorDe<MensagemDeVendaGanha>
{
    /// <summary>O maior nome de cronograma que o planejamento aceita.</summary>
    public const int MaiorNome = 160;

    public async Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var daVenda = construtora.Id
            ?? throw new InvalidOperationException("A venda ganha é tratada em nome da construtora dela, e este escopo não assumiu nenhuma.");

        var cronograma = await planejamento.AbrirCronograma(daVenda, NomeDaObra(mensagem.Titulo), Chave(mensagem.Oportunidade), cancelamento);

        Activity.Current?.SetTag("crm.planejamento.cronograma.id", cronograma.Id.ToString());
        VirouObra(registro, mensagem.Oportunidade, cronograma.Id, cronograma.Repetido);
    }

    /// <summary>A chave de idempotência do pedido: a venda — uma venda ganha, uma obra.</summary>
    public static string Chave(Guid oportunidade) => $"crm-obras:venda-ganha:{oportunidade}";

    /// <summary>
    /// A obra, como a venda se chamava quando foi ganha. A venda aceita
    /// título maior do que o planejamento aceita nome: o que passa é cortado,
    /// com reticências.
    /// </summary>
    public static string NomeDaObra(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);

        var aparado = titulo.Trim();

        return aparado.Length <= MaiorNome ? aparado : string.Concat(aparado.AsSpan(0, MaiorNome - 1).TrimEnd(), "…");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "A venda {Venda} virou o cronograma {Cronograma} no planejamento-obras (já aberto antes: {Repetido}).")]
    private static partial void VirouObra(ILogger registro, Guid venda, Guid cronograma, bool repetido);
}
