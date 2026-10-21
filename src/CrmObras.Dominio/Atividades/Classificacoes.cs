using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Atividades;

/// <summary>A identidade de uma atividade.</summary>
public sealed record AtividadeId : Identificador
{
    public AtividadeId(Guid valor)
        : base(valor)
    {
    }

    public static AtividadeId Novo() => new(Guid.CreateVersion7());
}

/// <summary>O que se vai fazer com o cliente.</summary>
public enum TipoDeAtividade
{
    Ligacao,
    Email,
    Reuniao,

    /// <summary>Ir à obra ou ao cliente: a visita técnica, a apresentação na sede.</summary>
    Visita,

    /// <summary>Trabalho interno pela venda: o orçamento, o projeto, a resposta a uma dúvida.</summary>
    Tarefa,
}

/// <summary>Se a atividade ainda está por fazer, ou como terminou.</summary>
public enum SituacaoDaAtividade
{
    Pendente,

    /// <summary>Feita, com o resultado anotado.</summary>
    Concluida,

    /// <summary>Não vai ser feita, com o motivo anotado.</summary>
    Cancelada,
}

/// <summary>O que se marca na agenda.</summary>
/// <param name="Tipo">Ligação, e-mail, reunião, visita ou tarefa.</param>
/// <param name="Assunto">O que se vai tratar, em poucas palavras: "Apresentar o orçamento".</param>
/// <param name="PrevistaPara">Quando.</param>
/// <param name="Com">O contato do cliente com quem se vai falar, se há um.</param>
/// <param name="Detalhes">O que mais for preciso saber para fazer.</param>
public sealed record DadosDaAtividade(
    TipoDeAtividade Tipo,
    string Assunto,
    DateTimeOffset PrevistaPara,
    ContatoId? Com = null,
    string? Detalhes = null);
