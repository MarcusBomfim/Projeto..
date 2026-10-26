using CrmObras.Processamento.Planejamento;
using CrmObras.Processamento.Testes.Apoio;
using Microsoft.Extensions.Options;

namespace CrmObras.Processamento.Testes;

/*
 * A configuração do planejamento é conferida quando o host sobe: endereço
 * absoluto, emissor e audiência do token, e uma chave que sirva para o HS256.
 * O que não serve é dito de uma vez, com o nome de cada configuração.
 */
public sealed class OpcoesDoPlanejamentoTests
{
    private static OpcoesDoPlanejamento QueServe()
    {
        var opcoes = new OpcoesDoPlanejamento();
        AmbienteDoProcessador.Configurar(opcoes, new Uri("http://planejamento-api:8080/"));

        return opcoes;
    }

    [Fact]
    public void A_configuracao_completa_serve()
    {
        Assert.Empty(OpcoesDoPlanejamento.Problemas(QueServe()));
        Assert.True(new ValidacaoDoPlanejamento().Validate(name: null, QueServe()).Succeeded);
    }

    [Theory]
    [InlineData("planejamento-api:8080")]
    [InlineData("ftp://planejamento-api/")]
    public void O_endereco_e_absoluto_e_http(string endereco)
    {
        var opcoes = QueServe();
        opcoes.Endereco = new Uri(endereco, UriKind.RelativeOrAbsolute);

        Assert.StartsWith("Planejamento:Endereco", Assert.Single(OpcoesDoPlanejamento.Problemas(opcoes)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("não é Base64")]
    [InlineData("Y2hhdmUgY3VydGEgZGVtYWlz")]
    public void A_chave_e_base64_de_pelo_menos_32_bytes(string chave)
    {
        var opcoes = QueServe();
        opcoes.Chave = chave;

        Assert.Equal(
            "Planejamento:Chave é a chave do token de serviço, em Base64, com 32 bytes ou mais.",
            Assert.Single(OpcoesDoPlanejamento.Problemas(opcoes)));
    }

    [Fact]
    public void Tudo_o_que_nao_serve_e_dito_de_uma_vez()
    {
        var resultado = new ValidacaoDoPlanejamento().Validate(name: null, new OpcoesDoPlanejamento { TempoLimite = TimeSpan.Zero });

        Assert.True(resultado.Failed);
        Assert.Equal(
            ["Endereco", "Emissor", "Audiencia", "Chave", "TempoLimite"],
            resultado.Failures!.Select(falha => falha["Planejamento:".Length..falha.IndexOf(' ', StringComparison.Ordinal)]));
    }

    [Fact]
    public void O_token_de_servico_e_em_nome_de_uma_construtora()
    {
        var tokens = new TokenDeServico(Options.Create(QueServe()), TimeProvider.System);

        Assert.Throws<ArgumentException>(() => tokens.Para(Guid.Empty));
        Assert.Equal(3, tokens.Para(Guid.CreateVersion7()).Split('.').Length);
    }
}
