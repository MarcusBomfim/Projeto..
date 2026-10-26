using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Infraestrutura.Mensagens;

namespace CrmObras.Infraestrutura.Testes.Mensagens;

/*
 * O formato publicado da venda ganha (ADR-0016): escrito por extenso, como
 * quem trata a mensagem a lê, e lido de volta com a mesma leitura estrita dos
 * documentos — tipo, formato e campo que esta versão não conhece são
 * recusados, e não adivinhados.
 */
public sealed class CatalogoDeMensagensTests
{
    private static readonly DateTimeOffset Ganho = new(2026, 11, 8, 9, 0, 0, TimeSpan.FromHours(-3));

    private static VendaGanha UmaVendaGanha()
    {
        var funil = Funil.PadraoDaConstrutora();
        var venda = VendasDeTeste.DoGalpao(funil, VendasDeTeste.RotaSul());
        venda.Ganhar(funil, Dinheiro.Reais(2_350_000.50m), Ganho);

        return Assert.IsType<VendaGanha>(Assert.Single(venda.Eventos));
    }

    [Fact]
    public void A_venda_ganha_e_escrita_por_extenso()
    {
        var ganha = UmaVendaGanha();

        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(ganha);
        using var json = System.Text.Json.JsonDocument.Parse(conteudo);
        var campos = json.RootElement.EnumerateObject().ToDictionary(campo => campo.Name, campo => campo.Value);

        Assert.Equal("venda-ganha", tipo);
        Assert.Equal(["oportunidade", "conta", "titulo", "valorFechado", "ganhaEm"], campos.Keys);
        Assert.Equal(
            (ganha.Oportunidade.ToString(), ganha.Conta.ToString(), "Galpão de 1.200 m² em Jundiaí", "2350000.50", "2026-11-08T09:00:00-03:00"),
            (campos["oportunidade"].GetString(), campos["conta"].GetString(), campos["titulo"].GetString(), campos["valorFechado"].GetRawText(), campos["ganhaEm"].GetString()));
    }

    [Fact]
    public void A_venda_ganha_volta_igual()
    {
        var ganha = UmaVendaGanha();
        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(ganha);

        Assert.Equal(
            new MensagemDeVendaGanha(ganha.Oportunidade.Valor, ganha.Conta.Valor, ganha.Titulo, 2_350_000.50m, Ganho),
            CatalogoDeMensagens.Ler(tipo, CatalogoDeMensagens.Formato, conteudo));
    }

    [Fact]
    public void Formato_de_outra_versao_e_recusado()
    {
        var (tipo, conteudo) = CatalogoDeMensagens.Escrever(UmaVendaGanha());

        var erro = Assert.Throws<InvalidOperationException>(() => CatalogoDeMensagens.Ler(tipo, 2, conteudo));

        Assert.Equal("A mensagem venda-ganha está no formato 2, e esta versão do sistema lê o formato 1.", erro.Message);
    }

    [Fact]
    public void Tipo_que_esta_versao_nao_conhece_e_recusado()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => CatalogoDeMensagens.Ler("venda-perdida", 1, "{}"));

        Assert.Equal("Tipo de mensagem desconhecido: venda-perdida.", erro.Message);
    }

    [Theory]
    [InlineData("""{"oportunidade":"01a16700-0000-7000-8000-000000000001","conta":"01a16700-0000-7000-8000-000000000002","titulo":"Galpão","valorFechado":1,"ganhaEm":"2026-11-08T09:00:00-03:00","desconto":5}""")]
    [InlineData("""{"oportunidade":"01a16700-0000-7000-8000-000000000001","conta":"01a16700-0000-7000-8000-000000000002","valorFechado":1,"ganhaEm":"2026-11-08T09:00:00-03:00"}""")]
    [InlineData("""{"oportunidade":"01a16700-0000-7000-8000-000000000001","conta":"01a16700-0000-7000-8000-000000000002","titulo":null,"valorFechado":1,"ganhaEm":"2026-11-08T09:00:00-03:00"}""")]
    public void Conteudo_com_campo_a_mais_a_menos_ou_nulo_e_recusado(string conteudo) =>
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => CatalogoDeMensagens.Ler("venda-ganha", 1, conteudo));
}
