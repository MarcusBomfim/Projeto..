using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;

namespace CrmObras.Dominio.Oportunidades;

/// <summary>A identidade de uma oportunidade.</summary>
public sealed record OportunidadeId : Identificador
{
    public OportunidadeId(Guid valor)
        : base(valor)
    {
    }

    public static OportunidadeId Novo() => new(Guid.CreateVersion7());
}

/// <summary>Se a venda está em andamento, ou como terminou.</summary>
public enum SituacaoDaOportunidade
{
    /// <summary>Andando pelas etapas abertas do funil.</summary>
    Aberta,

    /// <summary>Vendida, pelo valor fechado. Não se reabre: virou obra.</summary>
    Ganha,

    /// <summary>Não vendida, com o motivo registrado. Pode ser reaberta.</summary>
    Perdida,
}

/// <summary>Por que a venda não fechou — o que diz onde o comercial perde obra.</summary>
public enum MotivoDaPerda
{
    Preco,
    Prazo,

    /// <summary>O cliente fechou com outra construtora: a perda diz com qual.</summary>
    ConcorrenteEscolhido,

    /// <summary>O cliente desistiu da obra.</summary>
    ProjetoCancelado,

    /// <summary>O financiamento não saiu.</summary>
    SemCredito,

    SemResposta,

    /// <summary>Qualquer outro, com a observação dizendo qual.</summary>
    Outro,
}

/// <summary>Como a venda foi perdida.</summary>
/// <param name="Motivo">Por que não fechou.</param>
/// <param name="Concorrente">Com quem o cliente fechou, se fechou com outro. Obrigatório quando o motivo é o concorrente.</param>
/// <param name="Observacao">O que mais se souber. Obrigatória quando o motivo é outro.</param>
public sealed record Perda(MotivoDaPerda Motivo, string? Concorrente = null, string? Observacao = null);

/// <summary>Uma estada da venda numa etapa: quando entrou e, se já saiu, quando saiu.</summary>
public sealed record PassagemPorEtapa(EtapaId Etapa, DateTimeOffset EntrouEm, DateTimeOffset? SaiuEm = null)
{
    /// <summary>Quanto durou a passagem — ou, se a venda ainda está na etapa, quanto dura até agora.</summary>
    public TimeSpan Duracao(DateTimeOffset agora) => (SaiuEm ?? agora) - EntrouEm;
}

/// <summary>Quanto tempo a venda passou numa etapa, somadas todas as passagens por ela.</summary>
public sealed record TempoNaEtapa(EtapaId Etapa, TimeSpan Tempo);
