using CrmObras.Dominio.Comum;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes.MeiosDeContato;

public sealed class TelefoneTests
{
    [Theory]
    [InlineData("(11) 98765-4321", "(11) 98765-4321", true)]
    [InlineData("11987654321", "(11) 98765-4321", true)]
    [InlineData("+55 11 98765-4321", "(11) 98765-4321", true)]
    [InlineData("55 11 98765 4321", "(11) 98765-4321", true)]
    [InlineData("011 98765-4321", "(11) 98765-4321", true)]
    [InlineData("(55) 3222-1111", "(55) 3222-1111", false)]
    [InlineData("+55 55 3222-1111", "(55) 3222-1111", false)]
    [InlineData("021.2555.0000", "(21) 2555-0000", false)]
    public void Le_o_que_as_pessoas_escrevem(string texto, string formatado, bool celular)
    {
        var telefone = Telefone.De(texto);

        Assert.Equal((formatado, celular), (telefone.Formatado, telefone.EhCelular));
    }

    [Fact]
    public void O_mesmo_numero_escrito_de_outro_jeito_e_o_mesmo_telefone()
    {
        Assert.Equal(Telefone.De("(11) 98765-4321"), Telefone.De("+5511987654321"));
        Assert.Equal("+5511987654321", Telefone.De("(11) 98765-4321").Internacional);
    }

    [Theory]
    [InlineData("(20) 98765-4321", "O DDD 20 não existe.")]
    [InlineData("(36) 3333-4444", "O DDD 36 não existe.")]
    [InlineData("(11) 88765-4321", "\"(11) 88765-4321\" não é um celular: o celular começa com 9.")]
    [InlineData("(11) 9876-4321", "\"(11) 9876-4321\" não é um telefone fixo: o fixo começa de 2 a 5.")]
    [InlineData("(11) 1333-4444", "\"(11) 1333-4444\" não é um telefone fixo: o fixo começa de 2 a 5.")]
    [InlineData("0800 123 4567", "0800 123 4567 é número de serviço, e não telefone de contato.")]
    [InlineData("98765-4321", "\"98765-4321\" não é um telefone: são o DDD e 8 dígitos no fixo, ou 9 no celular.")]
    [InlineData("(11) 98765-4321 r. 20", "\"(11) 98765-4321 r. 20\" não é um telefone: só dígitos, com parênteses, traço ou espaço.")]
    public void Recusa_dizendo_o_que_esta_errado(string texto, string mensagem)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Telefone.De(texto));

        Assert.Equal(mensagem, erro.Message);
    }
}
