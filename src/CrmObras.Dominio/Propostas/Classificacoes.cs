using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Propostas;

/// <summary>A identidade de uma proposta.</summary>
public sealed record PropostaId : Identificador
{
    public PropostaId(Guid valor)
        : base(valor)
    {
    }

    public static PropostaId Novo() => new(Guid.CreateVersion7());
}

/// <summary>O cargo no comercial — o que diz até quanto de desconto a pessoa dá sem pedir a ninguém.</summary>
public enum Cargo
{
    Vendedor,
    Gerente,
    Diretor,
}

/// <summary>Em que ponto está uma versão da proposta.</summary>
public enum SituacaoDaVersao
{
    /// <summary>Sendo redigida: muda à vontade, e o cliente ainda não viu.</summary>
    Rascunho,

    /// <summary>Com o cliente, esperando resposta.</summary>
    Enviada,

    /// <summary>O cliente aceitou: a proposta está fechada.</summary>
    Aceita,

    /// <summary>O cliente recusou, com o motivo; pode vir outra versão.</summary>
    Recusada,

    /// <summary>Uma versão mais nova foi enviada no lugar dela.</summary>
    Substituida,
}

/// <summary>Quem do comercial age sobre a proposta: o login e o cargo.</summary>
public sealed record Comercial
{
    private const int MaiorLogin = 100;

    public Comercial(string login, Cargo cargo)
    {
        if (Textos.Problema(login, "o login de quem age na proposta", MaiorLogin) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Login = login.Trim();
        Cargo = cargo;
    }

    public string Login { get; }

    public Cargo Cargo { get; }

    public override string ToString() => Login;
}

/// <summary>Quem aprovou o desconto acima da alçada de quem envia, e quando.</summary>
public sealed record AprovacaoDeDesconto(Comercial Aprovador, DateTimeOffset Em);

/// <summary>Uma linha da proposta, como se informa.</summary>
/// <param name="Descricao">O serviço: "Piso industrial em concreto armado, fck 30 MPa, 15 cm".</param>
/// <param name="Quantidade">Quanto: 1.200.</param>
/// <param name="Unidade">De quê: "m²", "m³", "vb", "un".</param>
/// <param name="PrecoUnitario">Quanto custa cada unidade, em centavos.</param>
public sealed record DadosDoItem(string Descricao, decimal Quantidade, string Unidade, Dinheiro PrecoUnitario);

/// <summary>O conteúdo de uma versão da proposta.</summary>
/// <param name="Itens">As linhas, na ordem em que o cliente lê.</param>
/// <param name="Desconto">Sobre o subtotal, em porcentagem, com até duas casas: 7,5.</param>
/// <param name="ValidaAte">Até quando o cliente pode aceitar.</param>
/// <param name="CondicoesDePagamento">Entrada, parcelas, medições: como o cliente paga.</param>
public sealed record DadosDaVersao(
    IReadOnlyList<DadosDoItem> Itens,
    decimal Desconto,
    DateOnly ValidaAte,
    string? CondicoesDePagamento = null);

/// <summary>Uma linha da proposta.</summary>
public sealed record ItemDaProposta
{
    internal ItemDaProposta(DadosDoItem dados)
    {
        Descricao = dados.Descricao.Trim();
        Quantidade = dados.Quantidade;
        Unidade = dados.Unidade.Trim();
        PrecoUnitario = dados.PrecoUnitario;
    }

    public string Descricao { get; }

    public decimal Quantidade { get; }

    public string Unidade { get; }

    public Dinheiro PrecoUnitario { get; }

    /// <summary>A quantidade vezes o preço, ao centavo: o valor que o cliente lê na linha, e que a soma usa.</summary>
    public Dinheiro Total => (PrecoUnitario * Quantidade).EmCentavos();
}
