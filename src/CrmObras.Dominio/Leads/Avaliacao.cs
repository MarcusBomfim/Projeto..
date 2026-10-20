namespace CrmObras.Dominio.Leads;

/// <summary>A resposta a um critério da qualificação.</summary>
public enum Resposta
{
    /// <summary>Ainda não se perguntou, ou o cliente não soube dizer.</summary>
    NaoSabido,

    Sim,

    Nao,
}

/// <summary>
/// Os quatro critérios da qualificação — o BANT do comercial, nas perguntas
/// da obra (ADR-0003).
/// </summary>
/// <param name="Orcamento">Há verba, ou financiamento aprovado, para a obra?</param>
/// <param name="FalaComQuemDecide">Quem conversa com a construtora é quem decide, ou fala por ele?</param>
/// <param name="Necessidade">A obra é real e definida — terreno, uso, tamanho —, e não uma curiosidade?</param>
/// <param name="Prazo">Há data para começar, nos próximos doze meses?</param>
public sealed record Avaliacao(Resposta Orcamento, Resposta FalaComQuemDecide, Resposta Necessidade, Resposta Prazo)
{
    /// <summary>Nenhuma pergunta respondida: como todo lead nasce.</summary>
    public static readonly Avaliacao NaoFeita = new(Resposta.NaoSabido, Resposta.NaoSabido, Resposta.NaoSabido, Resposta.NaoSabido);

    /// <summary>
    /// O que falta para o lead se qualificar: a necessidade e o orçamento
    /// confirmados, e quem decide na conversa ou um prazo definido. Vazio se
    /// não falta nada.
    /// </summary>
    public IReadOnlyList<string> Pendencias
    {
        get
        {
            var pendencias = new List<string>();

            if (Necessidade != Resposta.Sim)
            {
                pendencias.Add("a necessidade da obra confirmada");
            }

            if (Orcamento != Resposta.Sim)
            {
                pendencias.Add("o orçamento confirmado");
            }

            if (FalaComQuemDecide != Resposta.Sim && Prazo != Resposta.Sim)
            {
                pendencias.Add("falar com quem decide ou ter o prazo da obra");
            }

            return pendencias;
        }
    }

    public bool Qualifica => Pendencias.Count == 0;
}
