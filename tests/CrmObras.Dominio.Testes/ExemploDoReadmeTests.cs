using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Testes;

/*
 * O exemplo do README, passo a passo. Cada número e cada frase que o README
 * mostra está conferido aqui: se o código mudar e a página deixar de ser
 * verdade, o build quebra.
 */
public sealed class ExemploDoReadmeTests
{
    private static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Do_formulario_do_site_a_conta_da_filial()
    {
        // A construtora já atende a matriz da Rota Sul, com o diretor de obras de contato.
        var matriz = new Conta("Logística Rota Sul", Documento.De("11.222.333/0001-81"));
        matriz.AdicionarContato(new DadosDoContato(
            "Otávio Reis",
            Email.De("otavio.reis@rotasul.com.br"),
            Telefone.De("(11) 3333-4444"),
            "Diretor de obras",
            PapelNaCompra.Decisor));

        // Pelo site chega a Marina, da filial de Campinas, pedindo um galpão.
        var lead = new Lead(
            new DadosDoLead(
                "Marina Costa",
                "Rota Sul Logística — Campinas",
                Documento.De("11.222.333/0002-62"),
                Email.De("Marina.Costa@RotaSul.com.br"),
                Telefone.De("+55 19 99876-5432"),
                "Galpão de 1.200 m² em Jundiaí"),
            OrigemDoLead.Site,
            null,
            Segunda);
        Assert.Equal(("marina.costa@rotasul.com.br", "(19) 99876-5432"), (lead.Email?.Endereco, lead.Telefone?.Formatado));

        // Primeira ligação: há verba e a obra é real, mas a Marina não decide, e ainda não há data.
        lead.Avaliar(new Avaliacao(Resposta.Sim, Resposta.Nao, Resposta.Sim, Resposta.NaoSabido));
        var cedo = Assert.Throws<ExcecaoDeDominio>(() => lead.Qualificar(Segunda));
        Assert.Equal("O lead Marina Costa ainda não se qualifica: falta falar com quem decide ou ter o prazo da obra.", cedo.Message);

        // Segunda ligação: a obra começa em março.
        lead.Avaliar(lead.Avaliacao with { Prazo = Resposta.Sim });
        lead.Qualificar(Segunda.AddDays(2));
        Assert.Equal(SituacaoDoLead.Qualificado, lead.Situacao);

        // Antes de criar a conta, a busca de duplicados: a filial é da mesma empresa da matriz.
        var duplicados = BuscaDeDuplicados.Do(lead, [matriz]);
        var duplicado = Assert.Single(duplicados);
        Assert.Equal(
            [
                "a mesma empresa: 11.222.333/0001-81 tem a raiz do CNPJ do lead",
                "Otávio Reis também tem e-mail em rotasul.com.br",
            ],
            duplicado.Indicios.Select(indicio => indicio.Motivo));

        // A filial tem CNPJ próprio: é outra conta, e a matriz não a recebe por engano.
        var naMatriz = Assert.Throws<ExcecaoDeDominio>(() => lead.ConverterNaConta(matriz, Segunda.AddDays(2)));
        Assert.Equal(
            "O lead é do cliente com o documento 11.222.333/0002-62, e a conta Logística Rota Sul tem o documento 11.222.333/0001-81: não são o mesmo cliente.",
            naMatriz.Message);

        var filial = lead.ConverterEmContaNova(duplicados, Segunda.AddDays(2));
        Assert.Equal(
            ("Rota Sul Logística — Campinas", "11.222.333/0002-62", "Marina Costa"),
            (filial.Nome, filial.Documento?.Formatado, filial.Principal?.Nome));
        Assert.Equal((SituacaoDoLead.Convertido, filial.Id), (lead.Situacao, lead.Conta));
    }

    [Fact]
    public void O_cnpj_alfanumerico_e_conferido_como_o_numerico()
    {
        Assert.Equal("12.ABC.345/01DE-35", Documento.De("12abc34501de35").Formatado);

        var errado = Assert.Throws<ExcecaoDeDominio>(() => Documento.De("12.ABC.345/01DE-53"));
        Assert.Equal("O CNPJ 12.ABC.345/01DE-53 não confere: os dígitos verificadores não batem com o número.", errado.Message);
    }
}
