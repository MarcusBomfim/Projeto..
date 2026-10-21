using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Testes.Oportunidades;

/// <summary>A venda dos testes: o galpão da Rota Sul, no funil padrão, com o Carlos de vendedor.</summary>
internal static class VendasDeTeste
{
    public static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    public static readonly DateOnly Dezembro = new(2026, 12, 15);

    public static Conta RotaSul()
    {
        var conta = new Conta("Logística Rota Sul Ltda");
        conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina.costa@rotasul.com.br"), Papel: PapelNaCompra.Influenciador));
        conta.AdicionarContato(new DadosDoContato("Paulo Prado", Email.De("paulo.prado@rotasul.com.br"), Papel: PapelNaCompra.Decisor));

        return conta;
    }

    public static Oportunidade DoGalpao(Funil funil, Conta conta) =>
        new(conta.Id, "Galpão de 1.200 m² em Jundiaí", funil, "carlos.souza", Segunda);

    /// <summary>Com tudo o que as etapas do funil padrão pedem: valor, previsão e quem decide.</summary>
    public static Oportunidade Completa(Funil funil, Conta conta)
    {
        var oportunidade = DoGalpao(funil, conta);
        oportunidade.Estimar(Dinheiro.Reais(2_400_000m));
        oportunidade.PreverFechamento(Dezembro);
        oportunidade.DefinirQuemDecide(conta, conta.Contatos[1].Id);

        return oportunidade;
    }

    public static Etapa Chamada(this Funil funil, string nome) =>
        funil.Abertas.Concat(funil.Arquivadas).Single(etapa => etapa.Nome == nome);
}
