using CrmObras.Api.Recursos.Contas;
using CrmObras.Dominio.Leads;

namespace CrmObras.Api.Recursos.Leads;

/// <summary>
/// Quem procurou a construtora: o nome, um e-mail ou um telefone, e o que
/// mais deixou. Indicação diz quem indicou, e evento, qual evento, no
/// <c>detalheDaOrigem</c>.
/// </summary>
public sealed record NovoLead(
    string Nome,
    OrigemDoLead Origem,
    string? Empresa = null,
    string? Documento = null,
    string? Email = null,
    string? Telefone = null,
    string? Interesse = null,
    string? DetalheDaOrigem = null)
{
    public DadosDoLead ParaDados() =>
        new(Nome, Empresa, Documento.ComoDocumento(), Email.ComoEmail(), Telefone.ComoTelefone(), Interesse);
}

/// <summary>As respostas às quatro perguntas da qualificação (ADR-0003).</summary>
public sealed record NovaAvaliacao(Resposta Orcamento, Resposta FalaComQuemDecide, Resposta Necessidade, Resposta Prazo)
{
    public Avaliacao ParaAvaliacao() => new(Orcamento, FalaComQuemDecide, Necessidade, Prazo);
}

/// <summary>Por que o lead não segue. Motivo <c>Outro</c> diz qual, na observação.</summary>
public sealed record Descarte(MotivoDoDescarte Motivo, string? Observacao = null);

/// <summary>
/// Em que conta o lead vira cliente: numa que já existe, ou — sem conta —
/// numa nova, depois de o servidor procurar os duplicados.
/// </summary>
public sealed record Conversao(Guid? Conta = null);

/// <summary>Um lead na lista: o bastante para escolher em quem mexer, e a versão para mexer.</summary>
public sealed record ResumoDoLeadResposta(
    Guid Id,
    string Nome,
    string? Empresa,
    OrigemDoLead Origem,
    SituacaoDoLead Situacao,
    DateTimeOffset ChegouEm,
    int Versao);

public sealed record LeadResposta(
    Guid Id,
    string Nome,
    string? Empresa,
    string? Documento,
    string? Email,
    string? Telefone,
    string? Interesse,
    OrigemDoLead Origem,
    string? DetalheDaOrigem,
    DateTimeOffset ChegouEm,
    SituacaoDoLead Situacao,
    AvaliacaoResposta Avaliacao,
    DateTimeOffset? QualificadoEm,
    DescarteResposta? Descarte,
    ConversaoDoLeadResposta? Conversao)
{
    public static LeadResposta De(Lead lead)
    {
        ArgumentNullException.ThrowIfNull(lead);

        return new(
            lead.Id.Valor,
            lead.Nome,
            lead.Empresa,
            lead.Documento?.Formatado,
            lead.Email?.Endereco,
            lead.Telefone?.Formatado,
            lead.Interesse,
            lead.Origem,
            lead.DetalheDaOrigem,
            lead.ChegouEm,
            lead.Situacao,
            new AvaliacaoResposta(
                lead.Avaliacao.Orcamento,
                lead.Avaliacao.FalaComQuemDecide,
                lead.Avaliacao.Necessidade,
                lead.Avaliacao.Prazo,
                lead.Avaliacao.Qualifica,
                lead.Avaliacao.Pendencias),
            lead.QualificadoEm,
            lead is { MotivoDoDescarte: { } motivo, DescartadoEm: { } descartadoEm }
                ? new DescarteResposta(motivo, lead.ObservacaoDoDescarte, descartadoEm)
                : null,
            lead is { Conta: { } conta, Contato: { } contato, ConvertidoEm: { } convertidoEm }
                ? new ConversaoDoLeadResposta(conta.Valor, contato.Valor, convertidoEm)
                : null);
    }
}

/// <summary>As respostas da qualificação, se elas qualificam o lead, e o que falta, se não.</summary>
public sealed record AvaliacaoResposta(
    Resposta Orcamento,
    Resposta FalaComQuemDecide,
    Resposta Necessidade,
    Resposta Prazo,
    bool Qualifica,
    IReadOnlyList<string> Pendencias);

public sealed record DescarteResposta(MotivoDoDescarte Motivo, string? Observacao, DateTimeOffset Em);

/// <summary>A conta em que o lead virou cliente, e o contato que ele virou nela.</summary>
public sealed record ConversaoDoLeadResposta(Guid Conta, Guid Contato, DateTimeOffset Em);

/// <summary>Uma conta que pode ser o cliente do lead, e o que ela tem em comum com ele — do indício mais forte ao mais fraco.</summary>
public sealed record PossivelDuplicadoResposta(Guid Conta, string NomeDaConta, IReadOnlyList<IndicioResposta> Indicios)
{
    public static PossivelDuplicadoResposta De(PossivelDuplicado duplicado)
    {
        ArgumentNullException.ThrowIfNull(duplicado);

        return new(
            duplicado.Conta.Valor,
            duplicado.NomeDaConta,
            [.. duplicado.Indicios.Select(indicio => new IndicioResposta(indicio.Semelhanca, indicio.Motivo))]);
    }
}

public sealed record IndicioResposta(Semelhanca Semelhanca, string Motivo);

/// <summary>O lead convertido e a conta em que ele virou cliente, com a versão dela — para quem for mexer nela em seguida.</summary>
public sealed record ConversaoResposta(LeadResposta Lead, ContaResposta Conta, int VersaoDaConta);
