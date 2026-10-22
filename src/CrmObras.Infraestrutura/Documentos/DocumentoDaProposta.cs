using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;

namespace CrmObras.Infraestrutura.Documentos;

/// <summary>
/// Como uma proposta é gravada: cada versão, com os itens, o desconto, a
/// validade e o que aconteceu com ela. A venda e a conta são outros
/// agregados, referenciados pelas colunas <c>oportunidade_id</c> e
/// <c>conta_id</c>.
/// </summary>
internal sealed record DocumentoDaProposta
{
    public const short Formato = 1;

    public required DateTimeOffset CriadaEm { get; init; }

    public required IReadOnlyList<VersaoGravada> Versoes { get; init; }

    public static DocumentoDaProposta De(Proposta proposta) => new()
    {
        CriadaEm = proposta.CriadaEm,
        Versoes = [.. proposta.Versoes.Select(versao => VersaoGravada.De(versao.Estado))],
    };

    /// <remarks>O conteúdo de cada versão e a história que elas contam juntas são conferidos pelas regras (ADR-0010).</remarks>
    public Proposta ParaProposta(PropostaId id, OportunidadeId oportunidade, ContaId conta) =>
        Proposta.Reconstituir(id, oportunidade, conta, CriadaEm, [.. Versoes.Select(versao => versao.ParaEstado())]);
}

internal sealed record VersaoGravada
{
    public required int Numero { get; init; }

    public required IReadOnlyList<ItemGravado> Itens { get; init; }

    public required decimal Desconto { get; init; }

    public required DateOnly ValidaAte { get; init; }

    public required string? CondicoesDePagamento { get; init; }

    public required SituacaoDaVersao Situacao { get; init; }

    public required AprovacaoGravada? Aprovacao { get; init; }

    public required ComercialGravado? EnviadaPor { get; init; }

    public required DateTimeOffset? EnviadaEm { get; init; }

    public required DateTimeOffset? RespondidaEm { get; init; }

    public required string? MotivoDaRecusa { get; init; }

    public static VersaoGravada De(EstadoDaVersao versao) => new()
    {
        Numero = versao.Numero,
        Itens = [.. versao.Conteudo.Itens.Select(ItemGravado.De)],
        Desconto = versao.Conteudo.Desconto,
        ValidaAte = versao.Conteudo.ValidaAte,
        CondicoesDePagamento = versao.Conteudo.CondicoesDePagamento,
        Situacao = versao.Situacao,
        Aprovacao = versao.Aprovacao is { } aprovacao ? AprovacaoGravada.De(aprovacao) : null,
        EnviadaPor = versao.EnviadaPor is { } quem ? ComercialGravado.De(quem) : null,
        EnviadaEm = versao.EnviadaEm,
        RespondidaEm = versao.RespondidaEm,
        MotivoDaRecusa = versao.MotivoDaRecusa,
    };

    public EstadoDaVersao ParaEstado() =>
        new(
            Numero,
            new DadosDaVersao([.. Itens.Select(item => item.ParaDados())], Desconto, ValidaAte, CondicoesDePagamento),
            Situacao,
            Aprovacao?.ParaAprovacao(),
            EnviadaPor?.ParaComercial(),
            EnviadaEm,
            RespondidaEm,
            MotivoDaRecusa);
}

internal sealed record ItemGravado
{
    public required string Descricao { get; init; }

    public required decimal Quantidade { get; init; }

    public required string Unidade { get; init; }

    public required decimal PrecoUnitario { get; init; }

    public static ItemGravado De(DadosDoItem item) => new()
    {
        Descricao = item.Descricao,
        Quantidade = item.Quantidade,
        Unidade = item.Unidade,
        PrecoUnitario = item.PrecoUnitario.Valor,
    };

    public DadosDoItem ParaDados() => new(Descricao, Quantidade, Unidade, Dinheiro.Reais(PrecoUnitario));
}

internal sealed record ComercialGravado
{
    public required string Login { get; init; }

    public required Cargo Cargo { get; init; }

    public static ComercialGravado De(Comercial comercial) => new() { Login = comercial.Login, Cargo = comercial.Cargo };

    public Comercial ParaComercial() => new(Login, Cargo);
}

internal sealed record AprovacaoGravada
{
    public required ComercialGravado Aprovador { get; init; }

    public required DateTimeOffset Em { get; init; }

    public static AprovacaoGravada De(AprovacaoDeDesconto aprovacao) => new()
    {
        Aprovador = ComercialGravado.De(aprovacao.Aprovador),
        Em = aprovacao.Em,
    };

    public AprovacaoDeDesconto ParaAprovacao() => new(Aprovador.ParaComercial(), Em);
}
