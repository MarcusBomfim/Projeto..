using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Recursos.Atividades;

/// <summary>
/// O que se marca na agenda de quem pede: a conta, o que, quando e, se há,
/// com quem e para qual venda (ADR-0006).
/// </summary>
public sealed record NovaAtividade(
    Guid Conta,
    TipoDeAtividade Tipo,
    string Assunto,
    DateTimeOffset PrevistaPara,
    Guid? Com = null,
    string? Detalhes = null,
    Guid? Oportunidade = null);

/// <summary>Para quando a atividade vai. Para mais tarde, conta um adiamento.</summary>
public sealed record Reagendamento(DateTimeOffset Para);

/// <summary>O que aconteceu: o que foi dito, o que ficou combinado.</summary>
public sealed record Conclusao(string Resultado);

public sealed record Cancelamento(string Motivo);

public sealed record AtividadeResposta(
    Guid Id,
    Guid Conta,
    Guid? Oportunidade,
    TipoDeAtividade Tipo,
    string Assunto,
    Guid? Com,
    string? Detalhes,
    DateTimeOffset PrevistaPara,
    int Adiamentos,
    string Responsavel,
    SituacaoDaAtividade Situacao,
    bool Atrasada,
    string? Resultado,
    string? MotivoDoCancelamento,
    DateTimeOffset CriadaEm,
    DateTimeOffset? EncerradaEm)
{
    public static AtividadeResposta De(Atividade atividade, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(atividade);

        return new(
            atividade.Id.Valor,
            atividade.Conta.Valor,
            atividade.Oportunidade?.Valor,
            atividade.Tipo,
            atividade.Assunto,
            atividade.Com?.Valor,
            atividade.Detalhes,
            atividade.PrevistaPara,
            atividade.Adiamentos,
            atividade.Responsavel,
            atividade.Situacao,
            atividade.EstaAtrasada(agora),
            atividade.Resultado,
            atividade.MotivoDoCancelamento,
            atividade.CriadaEm,
            atividade.EncerradaEm);
    }
}

/// <summary>Uma atividade na lista, com a versão para mexer nela.</summary>
public sealed record ResumoDaAtividadeResposta(
    Guid Id,
    TipoDeAtividade Tipo,
    string Assunto,
    DateTimeOffset PrevistaPara,
    string Responsavel,
    SituacaoDaAtividade Situacao,
    Guid? Oportunidade,
    int Versao)
{
    public static ResumoDaAtividadeResposta De(Versionado<Atividade> lida)
    {
        ArgumentNullException.ThrowIfNull(lida);

        var atividade = lida.Agregado;

        return new(
            atividade.Id.Valor,
            atividade.Tipo,
            atividade.Assunto,
            atividade.PrevistaPara,
            atividade.Responsavel,
            atividade.Situacao,
            atividade.Oportunidade?.Valor,
            lida.Versao);
    }
}

/// <summary>
/// O dia de um vendedor: o que está atrasado, o que falta fazer hoje, e as
/// vendas dele sem próximo passo. O dia é o do fuso de quem olha.
/// </summary>
public sealed record AgendaResposta(
    DateOnly Dia,
    string Responsavel,
    IReadOnlyList<AtividadeNaAgendaResposta> Atrasadas,
    IReadOnlyList<AtividadeNaAgendaResposta> DeHoje,
    IReadOnlyList<VendaSemProximoPassoResposta> SemProximoPasso);

public sealed record AtividadeNaAgendaResposta(
    Guid Id,
    TipoDeAtividade Tipo,
    string Assunto,
    DateTimeOffset PrevistaPara,
    Guid Conta,
    string NomeDaConta,
    Guid? Oportunidade,
    int Adiamentos,
    int Versao);

public sealed record VendaSemProximoPassoResposta(
    Guid Id,
    string Titulo,
    Guid Conta,
    string NomeDaConta,
    Guid Etapa,
    DateTimeOffset EntrouNaEtapaEm,
    int Versao)
{
    public static VendaSemProximoPassoResposta De(Oportunidade venda, string nomeDaConta, int versao)
    {
        ArgumentNullException.ThrowIfNull(venda);

        return new(venda.Id.Valor, venda.Titulo, venda.Conta.Valor, nomeDaConta, venda.Etapa.Valor, venda.EntrouNaEtapaEm, versao);
    }
}
