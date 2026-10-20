using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Leads;

/// <summary>A identidade de um lead.</summary>
public sealed record LeadId : Identificador
{
    public LeadId(Guid valor)
        : base(valor)
    {
    }

    public static LeadId Novo() => new(Guid.CreateVersion7());
}

/// <summary>Por onde o lead chegou — o que diz em qual canal vale a pena investir.</summary>
public enum OrigemDoLead
{
    Site,

    /// <summary>Alguém indicou: o detalhe da origem diz quem.</summary>
    Indicacao,

    Telefone,

    /// <summary>Feira, estande, palestra: o detalhe da origem diz qual.</summary>
    Evento,

    RedesSociais,

    Outra,
}

/// <summary>Em que ponto o lead está.</summary>
public enum SituacaoDoLead
{
    /// <summary>Chegou, e ninguém conversou com ele ainda.</summary>
    Novo,

    /// <summary>Alguém está fazendo as perguntas da qualificação.</summary>
    EmQualificacao,

    /// <summary>Tem necessidade, orçamento e quem decida ou prazo: pronto para virar cliente.</summary>
    Qualificado,

    /// <summary>Não vai virar venda agora, e o motivo ficou registrado.</summary>
    Descartado,

    /// <summary>Virou conta e contato. Daqui em diante, a história é da conta.</summary>
    Convertido,
}

/// <summary>Por que o lead não segue — o que diz onde o comercial perde.</summary>
public enum MotivoDoDescarte
{
    SemOrcamento,
    SemInteresse,
    PrazoDistante,
    ForaDaRegiao,
    ConcorrenteEscolhido,
    NaoResponde,
    Duplicado,

    /// <summary>Qualquer outro, com a observação dizendo qual.</summary>
    Outro,
}
