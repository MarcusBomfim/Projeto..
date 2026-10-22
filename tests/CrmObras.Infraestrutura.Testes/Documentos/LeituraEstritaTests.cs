using System.Text.Json;
using System.Text.Json.Nodes;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Testes.Documentos;

/*
 * Documento que não é exatamente o formato esperado não vira, em silêncio,
 * um agregado com um campo zerado (ADR-0011). E o que se lê passa pelas
 * regras do domínio: um documento bem formado com um e-mail que não é e-mail
 * é recusado como seria na escrita (ADR-0010).
 */
public sealed class LeituraEstritaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Estragos => ["sem uma propriedade", "com uma propriedade que não existe", "nulo onde não cabe", "enum pelo número", "enum que não existe"];

    [Theory]
    [MemberData(nameof(Estragos))]
    public void Documento_fora_do_formato_nao_se_le(string estrago)
    {
        var registro = Gravada();
        var documento = JsonNode.Parse(registro.Documento)!.AsObject();
        var contato = documento["contatos"]![0]!.AsObject();

        switch (estrago)
        {
            case "sem uma propriedade":
                documento.Remove("principal");
                break;
            case "com uma propriedade que não existe":
                contato["apelido"] = "Ma";
                break;
            case "nulo onde não cabe":
                documento["nome"] = null;
                break;
            case "enum pelo número":
                contato["papel"] = 1;
                break;
            case "enum que não existe":
                contato["papel"] = "Padrinho";
                break;
        }

        registro.Documento = documento.ToJsonString();

        Assert.Throws<JsonException>(() => registro.ParaConta());
    }

    [Fact]
    public void O_documento_intacto_se_le()
    {
        var registro = Gravada();

        Assert.Equal("Rota Sul", registro.ParaConta().Nome);
    }

    [Fact]
    public void Documento_de_outro_formato_nao_se_le()
    {
        var registro = Gravada();
        registro.Formato = 2;

        var erro = Assert.Throws<InvalidOperationException>(() => registro.ParaConta());

        Assert.Equal($"O documento da conta {registro.Id} está no formato 2, e esta versão do sistema lê o formato 1.", erro.Message);
    }

    [Fact]
    public void O_que_se_le_passa_pelas_regras_do_dominio()
    {
        var registro = Gravada();
        var documento = JsonNode.Parse(registro.Documento)!.AsObject();
        documento["contatos"]![0]!["email"] = "marina.rotasul.com.br";
        registro.Documento = documento.ToJsonString();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.ParaConta());

        Assert.Equal("\"marina.rotasul.com.br\" não é um e-mail: falta o nome, a arroba ou um domínio como empresa.com.br.", erro.Message);
    }

    private static RegistroDeConta Gravada()
    {
        var conta = new Conta("Rota Sul");
        conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina@rotasul.com.br"), Papel: PapelNaCompra.Decisor));

        return RegistroDeConta.De(conta, 1, Agora);
    }
}
