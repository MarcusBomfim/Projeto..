using CrmObras.Dominio.Funis;

namespace CrmObras.Api.Recursos.Funis;

/// <summary>
/// O que a oportunidade precisa ter para entrar numa etapa. No contrato, uma
/// lista — <c>["ValorEstimado", "PrevisaoDeFechamento"]</c> —, e não a soma
/// de bits que o domínio guarda.
/// </summary>
public enum Requisito
{
    /// <summary>Quanto a obra deve valer.</summary>
    ValorEstimado,

    /// <summary>Quando a venda deve fechar.</summary>
    PrevisaoDeFechamento,

    /// <summary>Quem decide a compra, entre os contatos do cliente.</summary>
    QuemDecide,
}

/// <summary>Um funil novo: o nome e as etapas abertas, na ordem da venda. A Ganha e a Perdida o funil cria.</summary>
public sealed record NovoFunil(string Nome, IReadOnlyList<NovaEtapa> Etapas);

/// <summary>
/// Uma etapa aberta: o nome, a chance de fechar — de 1 a 99 —, quantos dias a
/// venda costuma ficar nela e o que a oportunidade precisa ter para entrar.
/// </summary>
public sealed record NovaEtapa(string Nome, int Probabilidade, int? DiasEsperados = null, IReadOnlyList<Requisito>? Requisitos = null)
{
    public DadosDaEtapa ParaDados() => new(Nome, Probabilidade, DiasEsperados, ListaDeRequisitos.Juntar(Requisitos));
}

/// <summary>A posição nova da etapa entre as abertas; a primeira é a 1.</summary>
public sealed record NovaPosicao(int Posicao);

/// <summary>As etapas em uso na ordem da venda — as abertas, a Ganha e a Perdida —, e as que saíram do funil.</summary>
public sealed record FunilResposta(Guid Id, string Nome, IReadOnlyList<EtapaResposta> Etapas, IReadOnlyList<EtapaResposta> Arquivadas)
{
    public static FunilResposta De(Funil funil)
    {
        ArgumentNullException.ThrowIfNull(funil);

        return new(
            funil.Id.Valor,
            funil.Nome,
            [.. funil.Abertas.Append(funil.Ganha).Append(funil.Perdida).Select(EtapaResposta.De)],
            [.. funil.Arquivadas.Select(EtapaResposta.De)]);
    }
}

/// <summary>Uma etapa, com os requisitos dela só: para entrar, valem também os das abertas antes dela.</summary>
public sealed record EtapaResposta(
    Guid Id,
    string Nome,
    TipoDeEtapa Tipo,
    int Probabilidade,
    int? DiasEsperados,
    IReadOnlyList<Requisito> Requisitos)
{
    public static EtapaResposta De(Etapa etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        return new(etapa.Id.Valor, etapa.Nome, etapa.Tipo, etapa.Probabilidade, etapa.DiasEsperados, ListaDeRequisitos.Separar(etapa.Requisitos));
    }
}

/// <summary>A lista de requisitos do contrato, e a soma de bits do domínio.</summary>
internal static class ListaDeRequisitos
{
    private static readonly (Requisito Requisito, Requisitos NoDominio)[] Pares =
    [
        (Requisito.ValorEstimado, Requisitos.ValorEstimado),
        (Requisito.PrevisaoDeFechamento, Requisitos.PrevisaoDeFechamento),
        (Requisito.QuemDecide, Requisitos.QuemDecide),
    ];

    /// <summary>A soma dos requisitos da lista; repetir um não muda nada.</summary>
    public static Requisitos Juntar(IReadOnlyList<Requisito>? lista) =>
        Pares
            .Where(par => lista?.Contains(par.Requisito) == true)
            .Aggregate(Requisitos.Nenhum, (todos, par) => todos | par.NoDominio);

    /// <summary>Os requisitos da soma, na ordem do contrato.</summary>
    public static IReadOnlyList<Requisito> Separar(Requisitos requisitos) =>
        [.. Pares.Where(par => requisitos.HasFlag(par.NoDominio)).Select(par => par.Requisito)];
}
