using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Propostas;

/// <summary>Uma versão da proposta: os itens, o desconto, a validade e o que aconteceu com ela.</summary>
/// <remarks>
/// Só muda pela proposta dela: é a proposta que sabe qual versão está em
/// rascunho, qual está com o cliente e se o desconto cabe na alçada.
/// </remarks>
public sealed class VersaoDaProposta
{
    private List<ItemDaProposta> _itens = [];

    internal VersaoDaProposta(int numero, DadosDaVersao dados)
    {
        Numero = numero;
        Redigir(dados);
    }

    /// <summary>1, 2, 3: a ordem em que as versões foram criadas.</summary>
    public int Numero { get; }

    public IReadOnlyList<ItemDaProposta> Itens => _itens;

    /// <summary>Sobre o subtotal, em porcentagem.</summary>
    public decimal Desconto { get; private set; }

    public DateOnly ValidaAte { get; private set; }

    public string? CondicoesDePagamento { get; private set; }

    public SituacaoDaVersao Situacao { get; private set; }

    /// <summary>A aprovação do desconto acima da alçada de quem envia. Redigir de novo a desfaz.</summary>
    public AprovacaoDeDesconto? Aprovacao { get; private set; }

    public Comercial? EnviadaPor { get; private set; }

    public DateTimeOffset? EnviadaEm { get; private set; }

    /// <summary>Quando o cliente aceitou ou recusou.</summary>
    public DateTimeOffset? RespondidaEm { get; private set; }

    public string? MotivoDaRecusa { get; private set; }

    /// <summary>A soma das linhas, cada uma já ao centavo.</summary>
    public Dinheiro Subtotal => _itens.Aggregate(Dinheiro.Zero, (soma, item) => soma + item.Total);

    /// <summary>O desconto em reais, ao centavo.</summary>
    public Dinheiro ValorDoDesconto => (Subtotal * (Desconto / 100m)).EmCentavos();

    /// <summary>O que o cliente paga: o subtotal menos o desconto.</summary>
    public Dinheiro Total => Subtotal - ValorDoDesconto;

    public override string ToString() => $"versão {Numero}";

    /// <summary>O conteúdo da versão, para começar outra igual.</summary>
    internal DadosDaVersao Conteudo() => new(
        [.. _itens.Select(item => new DadosDoItem(item.Descricao, item.Quantidade, item.Unidade, item.PrecoUnitario))],
        Desconto,
        ValidaAte,
        CondicoesDePagamento);

    /// <summary>Tudo o que a versão é, para gravar e refazer (ADR-0010).</summary>
    internal EstadoDaVersao Estado =>
        new(Numero, Conteudo(), Situacao, Aprovacao, EnviadaPor, EnviadaEm, RespondidaEm, MotivoDaRecusa);

    /// <summary>A versão gravada, com o conteúdo e o que aconteceu com ela; quem confere a coerência é a proposta.</summary>
    internal static VersaoDaProposta Reconstituir(EstadoDaVersao estado)
    {
        return new VersaoDaProposta(estado.Numero, estado.Conteudo)
        {
            Situacao = estado.Situacao,
            Aprovacao = estado.Aprovacao,
            EnviadaPor = estado.EnviadaPor,
            EnviadaEm = estado.EnviadaEm,
            RespondidaEm = estado.RespondidaEm,
            MotivoDaRecusa = estado.MotivoDaRecusa,
        };
    }

    internal void Redigir(DadosDaVersao dados)
    {
        _itens = [.. dados.Itens.Select(item => new ItemDaProposta(item))];
        Desconto = dados.Desconto;
        ValidaAte = dados.ValidaAte;
        CondicoesDePagamento = Textos.Opcional(dados.CondicoesDePagamento);
        Aprovacao = null;
    }

    internal void Aprovar(AprovacaoDeDesconto aprovacao) => Aprovacao = aprovacao;

    internal void Enviar(Comercial quem, DateTimeOffset em)
    {
        Situacao = SituacaoDaVersao.Enviada;
        EnviadaPor = quem;
        EnviadaEm = em;
    }

    internal void Substituir() => Situacao = SituacaoDaVersao.Substituida;

    internal void Aceitar(DateTimeOffset em)
    {
        Situacao = SituacaoDaVersao.Aceita;
        RespondidaEm = em;
    }

    internal void Recusar(string motivo, DateTimeOffset em)
    {
        Situacao = SituacaoDaVersao.Recusada;
        MotivoDaRecusa = motivo;
        RespondidaEm = em;
    }
}

/// <summary>Tudo o que uma versão da proposta é: o que se grava, e de onde ela é refeita.</summary>
internal sealed record EstadoDaVersao(
    int Numero,
    DadosDaVersao Conteudo,
    SituacaoDaVersao Situacao,
    AprovacaoDeDesconto? Aprovacao = null,
    Comercial? EnviadaPor = null,
    DateTimeOffset? EnviadaEm = null,
    DateTimeOffset? RespondidaEm = null,
    string? MotivoDaRecusa = null);
