using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Oportunidades;

namespace CrmObras.Dominio.Testes.Propostas;

/// <summary>A proposta dos testes: o galpão da Rota Sul, em três linhas, com o Carlos de vendedor.</summary>
internal static class PropostasDeTeste
{
    public static readonly DateTimeOffset Segunda = VendasDeTeste.Segunda;

    public static readonly DateOnly Validade = new(2026, 11, 30);

    public static readonly Comercial Carlos = new("carlos.souza", Cargo.Vendedor);

    public static readonly Comercial Helena = new("helena.martins", Cargo.Gerente);

    public static readonly Comercial Roberto = new("roberto.alves", Cargo.Diretor);

    public static DadosDoItem Piso => new("Piso industrial em concreto armado, fck 30 MPa, 15 cm", 1_200m, "m²", Dinheiro.Reais(185.50m));

    public static DadosDoItem Estrutura => new("Estrutura metálica do galpão", 1m, "vb", Dinheiro.Reais(1_450_000m));

    public static DadosDoItem Cobertura => new("Cobertura em telha termoacústica", 1_350m, "m²", Dinheiro.Reais(312.40m));

    /// <summary>Piso, estrutura e cobertura: R$ 2.094.340,00 antes do desconto.</summary>
    public static DadosDaVersao DoGalpao(decimal desconto = 0m) => new([Piso, Estrutura, Cobertura], desconto, Validade, "30% na assinatura e o resto por medição");

    public static Oportunidade Venda() => VendasDeTeste.DoGalpao(Funil.PadraoDaConstrutora(), VendasDeTeste.RotaSul());

    public static Proposta DoGalpao(Oportunidade venda, decimal desconto = 0m) => new(venda, DoGalpao(desconto), Segunda);

    public static Proposta Enviada(decimal desconto = 0m)
    {
        var proposta = DoGalpao(Venda(), desconto);
        proposta.Enviar(Roberto, Alcadas.Padrao, Segunda.AddDays(1));

        return proposta;
    }
}
