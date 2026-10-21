using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;

namespace CrmObras.Dominio.Historico;

/// <summary>Uma coisa que aconteceu com o cliente, numa frase.</summary>
/// <param name="Em">Quando.</param>
/// <param name="Descricao">O que: "Proposta enviada: versão 2, R$ 1.843.019,20".</param>
/// <param name="Oportunidade">A venda a que diz respeito, se diz respeito a uma.</param>
public sealed record Acontecimento(DateTimeOffset Em, string Descricao, OportunidadeId? Oportunidade = null);

/// <summary>
/// O que aconteceu com o cliente, do mais recente para o mais antigo: as
/// vendas, as atividades feitas e as propostas.
/// </summary>
/// <remarks>
/// <para>
/// É o que quem assume a conta lê primeiro. Nada é guardado à parte: a linha
/// do tempo sai das passagens das vendas pelas etapas, das atividades
/// concluídas e das versões enviadas e respondidas.
/// </para>
/// <para>
/// O que não aconteceu não entra — a atividade pendente, a cancelada, o
/// rascunho. E no mesmo instante, a venda vem antes da proposta: a venda
/// ganha é consequência da proposta aceita.
/// </para>
/// </remarks>
public static class LinhaDoTempo
{
    /// <param name="conta">O cliente. Só entra o que é dele.</param>
    /// <param name="funis">Os funis das vendas, para dizer o nome das etapas.</param>
    /// <param name="oportunidades">As vendas.</param>
    /// <param name="atividades">As atividades.</param>
    /// <param name="propostas">As propostas.</param>
    public static IReadOnlyList<Acontecimento> Da(
        Conta conta,
        IEnumerable<Funil> funis,
        IEnumerable<Oportunidade> oportunidades,
        IEnumerable<Atividade> atividades,
        IEnumerable<Proposta> propostas)
    {
        ArgumentNullException.ThrowIfNull(conta);
        ArgumentNullException.ThrowIfNull(funis);
        ArgumentNullException.ThrowIfNull(oportunidades);
        ArgumentNullException.ThrowIfNull(atividades);
        ArgumentNullException.ThrowIfNull(propostas);

        var porId = funis.ToDictionary(funil => funil.Id);

        return
        [
            .. oportunidades
                .Where(oportunidade => oportunidade.Conta == conta.Id)
                .SelectMany(oportunidade => DaVenda(oportunidade, FunilDa(oportunidade, porId)))
                .Concat(atividades.Where(atividade => atividade.Conta == conta.Id).SelectMany(DaAtividade))
                .Concat(propostas.Where(proposta => proposta.Conta == conta.Id).SelectMany(DaProposta))
                .OrderByDescending(acontecimento => acontecimento.Em),
        ];
    }

    private static Funil FunilDa(Oportunidade oportunidade, Dictionary<FunilId, Funil> funis) =>
        funis.TryGetValue(oportunidade.Funil, out var funil)
            ? funil
            : throw new ArgumentException($"Falta o funil da oportunidade {oportunidade.Titulo}.", nameof(funis));

    private static IEnumerable<Acontecimento> DaVenda(Oportunidade venda, Funil funil)
    {
        var passagens = venda.Passagens;

        yield return new Acontecimento(venda.CriadaEm, $"Venda aberta: {venda.Titulo}, em {funil.Etapa(passagens[0].Etapa)}", venda.Id);

        for (var posicao = 1; posicao < passagens.Count; posicao++)
        {
            var etapa = funil.Etapa(passagens[posicao].Etapa);
            var ultima = posicao == passagens.Count - 1;

            var descricao = etapa.Tipo switch
            {
                TipoDeEtapa.Ganha => $"Venda ganha: {venda.Titulo}, por {venda.ValorFechado}",
                TipoDeEtapa.Perdida when ultima && venda.Perda is { } perda => $"Venda perdida: {venda.Titulo}, {Motivo(perda)}",
                TipoDeEtapa.Perdida => $"Venda perdida: {venda.Titulo}",
                _ when funil.Etapa(passagens[posicao - 1].Etapa).Tipo == TipoDeEtapa.Perdida => $"Venda reaberta: {venda.Titulo}, em {etapa}",
                _ => $"{venda.Titulo}: entrou em {etapa}",
            };

            yield return new Acontecimento(passagens[posicao].EntrouEm, descricao, venda.Id);
        }
    }

    private static IEnumerable<Acontecimento> DaAtividade(Atividade atividade)
    {
        if (atividade is { Situacao: SituacaoDaAtividade.Concluida, EncerradaEm: { } em })
        {
            yield return new Acontecimento(em, $"{Nome(atividade.Tipo)}: {atividade.Assunto} — {atividade.Resultado}", atividade.Oportunidade);
        }
    }

    private static IEnumerable<Acontecimento> DaProposta(Proposta proposta)
    {
        foreach (var versao in proposta.Versoes)
        {
            if (versao.EnviadaEm is { } enviadaEm)
            {
                var desconto = versao.Desconto > 0 ? $", com {FormatoBrasileiro.Porcentagem(versao.Desconto)} de desconto" : string.Empty;

                yield return new Acontecimento(enviadaEm, $"Proposta enviada: versão {versao.Numero}, {versao.Total}{desconto}", proposta.Oportunidade);
            }

            if (versao.RespondidaEm is { } respondidaEm)
            {
                yield return new Acontecimento(
                    respondidaEm,
                    versao.Situacao == SituacaoDaVersao.Aceita
                        ? $"Proposta aceita: versão {versao.Numero}, {versao.Total}"
                        : $"Proposta recusada: versão {versao.Numero} — {versao.MotivoDaRecusa}",
                    proposta.Oportunidade);
            }
        }
    }

    private static string Nome(TipoDeAtividade tipo) => tipo switch
    {
        TipoDeAtividade.Ligacao => "Ligação",
        TipoDeAtividade.Email => "E-mail",
        TipoDeAtividade.Reuniao => "Reunião",
        TipoDeAtividade.Visita => "Visita",
        _ => "Tarefa",
    };

    private static string Motivo(Perda perda) => perda.Motivo switch
    {
        MotivoDaPerda.Preco => "por preço",
        MotivoDaPerda.Prazo => "por prazo",
        MotivoDaPerda.ConcorrenteEscolhido => $"para {perda.Concorrente}",
        MotivoDaPerda.ProjetoCancelado => "porque o projeto foi cancelado",
        MotivoDaPerda.SemCredito => "por falta de crédito",
        MotivoDaPerda.SemResposta => "por falta de resposta",
        _ => $"por outro motivo: {perda.Observacao}",
    };
}
