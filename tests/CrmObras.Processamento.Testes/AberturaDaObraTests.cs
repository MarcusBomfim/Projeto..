using System.Net;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Planejamento;
using CrmObras.Processamento.Testes.Apoio;
using CrmObras.Processamento.Tratadores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrmObras.Processamento.Testes;

/*
 * A venda ganha vira obra (ADR-0017): o processador pede ao planejamento o
 * cronograma da obra, em nome da construtora da venda, com o token de
 * serviço e uma chave de idempotência feita da venda. O planejamento fora do
 * ar, lento ou recusando é falha do tratamento — a mensagem volta mais
 * tarde —, e a repetição nunca abre uma segunda obra.
 */
public sealed class AberturaDaObraTests(BancoDeTestes banco) : TesteComFila(banco)
{
    private async Task<MensagemNoBanco> UnicaMensagemDe(Oportunidade venda) =>
        Assert.Single(await CaixaNoBanco.DoAgregado(Banco, venda.Id.Valor));

    [Fact]
    public async Task A_venda_ganha_vira_o_cronograma_da_obra_no_planejamento()
    {
        var venda = await Vendas().GanharUma();
        using var registro = new RegistroEmMemoria();
        await using var processamento = Processamento(servicos => servicos.AddLogging(log => log.AddProvider(registro)));

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var pedido = Assert.Single(Planejamento.Recebidos);
        Assert.Equal(
            ("Galpão de 1.200 m² em Jundiaí", $"crm-obras:venda-ganha:{venda.Id}"),
            (pedido.Nome, pedido.ChaveDeIdempotencia));
        Assert.StartsWith("Bearer ", pedido.Autorizacao, StringComparison.Ordinal);
        Assert.NotNull((await UnicaMensagemDe(venda)).TratadaEm);

        var cronograma = Planejamento.Abertos[pedido.ChaveDeIdempotencia];
        Assert.Contains(
            $"A venda {venda.Id} virou o cronograma {cronograma} no planejamento-obras (já aberto antes: False).",
            registro.Entradas.Select(entrada => entrada.Mensagem));
    }

    [Fact]
    public async Task O_token_e_do_crm_em_nome_da_construtora_da_venda_com_o_papel_de_quem_planeja()
    {
        await Vendas().GanharUma();
        await using var processamento = Processamento();

        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var token = Assert.Single(Planejamento.Recebidos).Autorizacao["Bearer ".Length..];
        var validacao = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = AmbienteDoProcessador.Emissor,
            ValidAudience = AmbienteDoProcessador.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(AmbienteDoProcessador.Chave)),
            ValidateLifetime = false,
        });

        Assert.True(validacao.IsValid, validacao.Exception?.Message);
        var lido = (JsonWebToken)validacao.SecurityToken;
        Assert.Equal(
            (Construtora.ToString(), "planejador", "crm-obras", "CRM de Obras"),
            (lido.GetClaim("empresa").Value, lido.GetClaim("role").Value, lido.Subject, lido.GetClaim("name").Value));
        Assert.Equal(
            (Relogio.Agora.UtcDateTime, Relogio.Agora.UtcDateTime + TimeSpan.FromMinutes(5)),
            (lido.ValidFrom, lido.ValidTo));
    }

    [Fact]
    public async Task Com_o_planejamento_fora_do_ar_a_mensagem_volta_mais_tarde_e_a_obra_nasce_uma_vez()
    {
        var venda = await Vendas().GanharUma();
        Planejamento.FalharUmaVez(
            HttpStatusCode.ServiceUnavailable,
            """{"title":"O serviço está indisponível.","status":503,"detail":"O banco do planejamento está em manutenção."}""");
        await using var processamento = Processamento();

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var falhou = await UnicaMensagemDe(venda);
        Assert.Equal(
            (1, (DateTimeOffset?)null, "FalhaNoPlanejamento: O planejamento-obras respondeu 503 ao abrir o cronograma: O banco do planejamento está em manutenção."),
            (falhou.Tentativas, falhou.TratadaEm, falhou.UltimoErro));

        Relogio.Adiantar(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        // O mesmo pedido nas duas tentativas, e uma obra só.
        Assert.Equal(2, Planejamento.Recebidos.Count);
        Assert.Single(Planejamento.Recebidos.Select(pedido => (pedido.ChaveDeIdempotencia, pedido.Corpo)).Distinct());
        Assert.Single(Planejamento.Abertos);
        Assert.NotNull((await UnicaMensagemDe(venda)).TratadaEm);
    }

    [Fact]
    public async Task A_obra_aberta_sem_a_confirmacao_aqui_nao_nasce_de_novo_na_repeticao()
    {
        // O planejamento abriu o cronograma, e depois disso o tratamento
        // falhou aqui — o processador caiu, o banco caiu: a mensagem volta.
        var venda = await Vendas().GanharUma();
        using var registro = new RegistroEmMemoria();
        await using var processamento = Processamento(servicos => servicos
            .AddLogging(log => log.AddProvider(registro))
            .AddSingleton(new Falhas(vezes: 1))
            .TratarTambemCom<TratadorQueFalha>());

        await processamento.Processador.ProcessarPendentes(Cancelamento);
        Relogio.Adiantar(TimeSpan.FromSeconds(30));
        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var cronograma = Assert.Single(Planejamento.Abertos).Value;
        Assert.Equal(2, Planejamento.Recebidos.Count);
        Assert.NotNull((await UnicaMensagemDe(venda)).TratadaEm);
        Assert.Contains(
            $"A venda {venda.Id} virou o cronograma {cronograma} no planejamento-obras (já aberto antes: True).",
            registro.Entradas.Select(entrada => entrada.Mensagem));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "", "FalhaNoPlanejamento: O planejamento-obras respondeu 401 ao abrir o cronograma: Unauthorized")]
    [InlineData(HttpStatusCode.UnprocessableEntity, """{"title":"O pedido fere uma regra do negócio.","status":422}""", "FalhaNoPlanejamento: O planejamento-obras respondeu 422 ao abrir o cronograma: O pedido fere uma regra do negócio.")]
    [InlineData(HttpStatusCode.BadGateway, "<html>proxy fora do ar</html>", "FalhaNoPlanejamento: O planejamento-obras respondeu 502 ao abrir o cronograma: <html>proxy fora do ar</html>")]
    public async Task A_recusa_do_planejamento_fica_na_mensagem_com_o_que_ele_disse(HttpStatusCode status, string corpo, string erro)
    {
        var venda = await Vendas().GanharUma();
        Planejamento.FalharUmaVez(status, corpo, corpo.StartsWith('{') ? "application/problem+json" : "text/html");
        await using var processamento = Processamento();

        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var mensagem = await UnicaMensagemDe(venda);
        Assert.Equal((1, erro), (mensagem.Tentativas, mensagem.UltimoErro));
    }

    [Fact]
    public async Task O_planejamento_que_nao_responde_a_tempo_e_falha_do_tratamento_e_nao_parada_do_host()
    {
        var venda = await Vendas().GanharUma();
        Planejamento.DemorarUmaVez(TimeSpan.FromSeconds(10));
        await using var processamento = Processamento(configurarPlanejamento: opcoes => opcoes.TempoLimite = TimeSpan.FromMilliseconds(300));

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        var mensagem = await UnicaMensagemDe(venda);
        Assert.Equal(1, mensagem.Tentativas);
        Assert.StartsWith("TaskCanceledException:", mensagem.UltimoErro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sem_planejamento_no_endereco_a_mensagem_volta_mais_tarde()
    {
        var venda = await Vendas().GanharUma();
        await using var processamento = Processamento(configurarPlanejamento: opcoes => opcoes.Endereco = new Uri("http://127.0.0.1:1/"));

        Assert.Equal(1, await processamento.Processador.ProcessarPendentes(Cancelamento));

        Assert.StartsWith("HttpRequestException:", (await UnicaMensagemDe(venda)).UltimoErro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_obra_de_nome_maior_do_que_o_planejamento_aceita_vai_cortada()
    {
        var titulo = $"Centro de distribuição {new string('x', 160)} em Jundiaí";
        await Vendas().GanharUma(titulo);
        await using var processamento = Processamento();

        await processamento.Processador.ProcessarPendentes(Cancelamento);

        var nome = Assert.Single(Planejamento.Recebidos).Nome!;
        Assert.Equal((AberturaDaObra.MaiorNome, $"{titulo[..159]}…"), (nome.Length, nome));
    }

    [Theory]
    [InlineData("  Galpão de 1.200 m² em Jundiaí  ", "Galpão de 1.200 m² em Jundiaí")]
    [InlineData("Galpão", "Galpão")]
    public void O_nome_da_obra_e_o_titulo_da_venda_aparado(string titulo, string nome) =>
        Assert.Equal(nome, AberturaDaObra.NomeDaObra(titulo));

    [Fact]
    public void O_titulo_que_so_cabe_cortado_nao_termina_em_espaco_antes_das_reticencias()
    {
        var titulo = new string('a', 158) + " b" + new string('c', 20);

        Assert.Equal(new string('a', 158) + "…", AberturaDaObra.NomeDaObra(titulo));
    }

    [Fact]
    public async Task O_tratador_assinado_e_o_da_venda_ganha()
    {
        await using var processamento = Processamento();
        await using var escopo = processamento.NovoEscopo();

        Assert.IsType<AberturaDaObra>(Assert.Single(escopo.ServiceProvider.GetServices<ITratadorDe<Infraestrutura.Mensagens.MensagemDeVendaGanha>>()));
        Assert.Equal(TimeSpan.FromSeconds(10), escopo.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ClienteDoPlanejamento)).Timeout);
    }
}
