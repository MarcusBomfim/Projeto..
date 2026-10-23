using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Recursos.Propostas;

/// <summary>Uma proposta nova para a venda, com o conteúdo da primeira versão, em rascunho.</summary>
public sealed record NovaProposta(Guid Oportunidade, ConteudoDaVersao Versao);

/// <summary>
/// O conteúdo de uma versão: os itens, na ordem em que o cliente lê, o
/// desconto sobre o subtotal, em porcentagem, a validade e as condições de
/// pagamento.
/// </summary>
public sealed record ConteudoDaVersao(
    IReadOnlyList<ItemDoConteudo> Itens,
    decimal Desconto,
    DateOnly ValidaAte,
    string? CondicoesDePagamento = null)
{
    public DadosDaVersao ParaDados() =>
        new([.. Itens.Select(item => new DadosDoItem(item.Descricao, item.Quantidade, item.Unidade, Dinheiro.Reais(item.PrecoUnitario)))], Desconto, ValidaAte, CondicoesDePagamento);
}

/// <summary>Uma linha: o serviço, quanto, de quê, e o preço de cada unidade, em reais.</summary>
public sealed record ItemDoConteudo(string Descricao, decimal Quantidade, string Unidade, decimal PrecoUnitario);

/// <summary>Por que o cliente recusou a versão que estava com ele.</summary>
public sealed record Recusa(string Motivo);

/// <summary>
/// As versões da proposta, da primeira à última, e quais estão com o cliente
/// e aceita. <c>vencida</c>: a que está com o cliente passou da validade.
/// </summary>
public sealed record PropostaResposta(
    Guid Id,
    Guid Oportunidade,
    Guid Conta,
    DateTimeOffset CriadaEm,
    int? Vigente,
    int? Aceita,
    bool Vencida,
    IReadOnlyList<VersaoResposta> Versoes)
{
    public static PropostaResposta De(Proposta proposta, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        return new(
            proposta.Id.Valor,
            proposta.Oportunidade.Valor,
            proposta.Conta.Valor,
            proposta.CriadaEm,
            proposta.Vigente?.Numero,
            proposta.Aceita?.Numero,
            proposta.EstaVencida(agora),
            [.. proposta.Versoes.Select(VersaoResposta.De)]);
    }
}

/// <summary>Uma versão: o conteúdo, os valores como o cliente os lê, e o que aconteceu com ela.</summary>
public sealed record VersaoResposta(
    int Numero,
    SituacaoDaVersao Situacao,
    IReadOnlyList<ItemResposta> Itens,
    decimal Subtotal,
    decimal Desconto,
    decimal ValorDoDesconto,
    decimal Total,
    DateOnly ValidaAte,
    string? CondicoesDePagamento,
    AprovacaoResposta? Aprovacao,
    ComercialResposta? EnviadaPor,
    DateTimeOffset? EnviadaEm,
    DateTimeOffset? RespondidaEm,
    string? MotivoDaRecusa)
{
    public static VersaoResposta De(VersaoDaProposta versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        return new(
            versao.Numero,
            versao.Situacao,
            [.. versao.Itens.Select(item => new ItemResposta(item.Descricao, item.Quantidade, item.Unidade, item.PrecoUnitario.Valor, item.Total.Valor))],
            versao.Subtotal.Valor,
            versao.Desconto,
            versao.ValorDoDesconto.Valor,
            versao.Total.Valor,
            versao.ValidaAte,
            versao.CondicoesDePagamento,
            versao.Aprovacao is { } aprovacao ? new AprovacaoResposta(ComercialResposta.De(aprovacao.Aprovador), aprovacao.Em) : null,
            versao.EnviadaPor is { } quem ? ComercialResposta.De(quem) : null,
            versao.EnviadaEm,
            versao.RespondidaEm,
            versao.MotivoDaRecusa);
    }
}

/// <summary>Uma linha, com o total ao centavo: o que o cliente lê, e o que a soma usa.</summary>
public sealed record ItemResposta(string Descricao, decimal Quantidade, string Unidade, decimal PrecoUnitario, decimal Total);

/// <summary>Quem do comercial agiu: o login e o cargo.</summary>
public sealed record ComercialResposta(string Login, Cargo Cargo)
{
    public static ComercialResposta De(Comercial comercial)
    {
        ArgumentNullException.ThrowIfNull(comercial);

        return new(comercial.Login, comercial.Cargo);
    }
}

public sealed record AprovacaoResposta(ComercialResposta Aprovador, DateTimeOffset Em);

/// <summary>Uma proposta na lista, com a versão para mexer nela.</summary>
public sealed record ResumoDaPropostaResposta(Guid Id, int Versoes, SituacaoDaVersao Situacao, decimal Total, int Versao)
{
    public static ResumoDaPropostaResposta De(Versionado<Proposta> lida)
    {
        ArgumentNullException.ThrowIfNull(lida);

        var atual = lida.Agregado.Aceita ?? lida.Agregado.Atual;

        return new(lida.Agregado.Id.Valor, lida.Agregado.Versoes.Count, atual.Situacao, atual.Total.Valor, lida.Versao);
    }
}

/// <summary>A proposta aceita e a venda ganha por ela, com a versão da venda — para quem for mexer nela em seguida.</summary>
public sealed record AceiteResposta(PropostaResposta Proposta, OportunidadeResposta Oportunidade, int VersaoDaOportunidade);
