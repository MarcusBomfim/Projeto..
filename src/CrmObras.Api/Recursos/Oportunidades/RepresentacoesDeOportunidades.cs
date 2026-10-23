using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Recursos.Oportunidades;

/// <summary>
/// Uma venda nova, na primeira etapa do funil: a conta, o funil, a obra e, se
/// já se sabe, o valor, a previsão e quem decide. Quem pede é o responsável.
/// </summary>
public sealed record NovaOportunidade(
    Guid Conta,
    Guid Funil,
    string Titulo,
    decimal? ValorEstimado = null,
    DateOnly? PrevisaoDeFechamento = null,
    Guid? QuemDecide = null);

/// <summary>A venda que nasce do lead convertido, na conta em que ele virou cliente (ADR-0003).</summary>
public sealed record OportunidadeDoLead(Guid Lead, Guid Funil);

/// <summary>O que muda na venda; o que não vier fica como está.</summary>
public sealed record AlteracaoDaOportunidade(
    string? Titulo = null,
    decimal? ValorEstimado = null,
    DateOnly? PrevisaoDeFechamento = null,
    Guid? QuemDecide = null);

/// <summary>Quem passa a cuidar: o login.</summary>
public sealed record NovoResponsavel(string Responsavel);

/// <summary>A etapa aberta para onde a venda vai — ou volta, se foi perdida.</summary>
public sealed record EtapaDeDestino(Guid Etapa);

/// <summary>Por quanto a obra foi vendida, em reais.</summary>
public sealed record Ganho(decimal ValorFechado);

/// <summary>Por que a venda não fechou. Concorrente escolhido diz qual; outro motivo diz qual, na observação.</summary>
public sealed record PerdaDaVenda(MotivoDaPerda Motivo, string? Concorrente = null, string? Observacao = null)
{
    public Perda ParaPerda() => new(Motivo, Concorrente, Observacao);
}

public sealed record OportunidadeResposta(
    Guid Id,
    Guid Conta,
    Guid Funil,
    string Titulo,
    string Responsavel,
    DateTimeOffset CriadaEm,
    Guid? Lead,
    SituacaoDaOportunidade Situacao,
    Guid Etapa,
    DateTimeOffset EntrouNaEtapaEm,
    decimal? ValorEstimado,
    DateOnly? PrevisaoDeFechamento,
    Guid? QuemDecide,
    decimal? ValorFechado,
    PerdaResposta? Perda,
    DateTimeOffset? FechadaEm,
    IReadOnlyList<PassagemResposta> Passagens)
{
    public static OportunidadeResposta De(Oportunidade venda)
    {
        ArgumentNullException.ThrowIfNull(venda);

        return new(
            venda.Id.Valor,
            venda.Conta.Valor,
            venda.Funil.Valor,
            venda.Titulo,
            venda.Responsavel,
            venda.CriadaEm,
            venda.Lead?.Valor,
            venda.Situacao,
            venda.Etapa.Valor,
            venda.EntrouNaEtapaEm,
            venda.ValorEstimado?.Valor,
            venda.PrevisaoDeFechamento,
            venda.QuemDecide?.Valor,
            venda.ValorFechado?.Valor,
            venda.Perda is { } perda ? new PerdaResposta(perda.Motivo, perda.Concorrente, perda.Observacao) : null,
            venda.FechadaEm,
            [.. venda.Passagens.Select(passagem => new PassagemResposta(passagem.Etapa.Valor, passagem.EntrouEm, passagem.SaiuEm))]);
    }
}

public sealed record PerdaResposta(MotivoDaPerda Motivo, string? Concorrente, string? Observacao);

/// <summary>Uma estada da venda numa etapa. A última é a atual, sem saída.</summary>
public sealed record PassagemResposta(Guid Etapa, DateTimeOffset EntrouEm, DateTimeOffset? SaiuEm);

/// <summary>Uma venda na lista, com a versão para mexer nela.</summary>
public sealed record ResumoDaOportunidadeResposta(
    Guid Id,
    string Titulo,
    Guid Conta,
    Guid Funil,
    Guid Etapa,
    SituacaoDaOportunidade Situacao,
    string Responsavel,
    decimal? ValorEstimado,
    int Versao)
{
    public static ResumoDaOportunidadeResposta De(Versionado<Oportunidade> lida)
    {
        ArgumentNullException.ThrowIfNull(lida);

        var venda = lida.Agregado;

        return new(
            venda.Id.Valor,
            venda.Titulo,
            venda.Conta.Valor,
            venda.Funil.Valor,
            venda.Etapa.Valor,
            venda.Situacao,
            venda.Responsavel,
            venda.ValorEstimado?.Valor,
            lida.Versao);
    }
}

/// <summary>O funil como o comercial olha para ele: uma coluna por etapa, com as vendas abertas, quanto somam e quais estão paradas.</summary>
public sealed record QuadroResposta(Guid Funil, string Nome, decimal Total, decimal Ponderado, IReadOnlyList<ColunaResposta> Colunas);

/// <summary>Uma coluna do quadro. A etapa arquivada só aparece se ainda tem venda, no fim.</summary>
public sealed record ColunaResposta(
    Guid Etapa,
    string Nome,
    int Probabilidade,
    int? DiasEsperados,
    bool Arquivada,
    decimal Total,
    decimal Ponderado,
    int SemValor,
    IReadOnlyList<CartaoResposta> Oportunidades);

/// <summary>Uma venda no quadro, da que entrou na etapa há mais tempo — com a versão, para movê-la.</summary>
public sealed record CartaoResposta(
    Guid Id,
    string Titulo,
    Guid Conta,
    string NomeDaConta,
    string Responsavel,
    decimal? ValorEstimado,
    DateOnly? PrevisaoDeFechamento,
    DateTimeOffset EntrouNaEtapaEm,
    bool Parada,
    int Versao);
