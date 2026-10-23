using System.Net;
using CrmObras.Api.Recursos;
using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Clientes;

namespace CrmObras.Api.Testes;

/*
 * Os clientes pela API: a conta com o documento que a identifica e as
 * pessoas com quem se fala lá dentro (ADR-0002).
 */
public sealed class ContasTests(ApiDeTestes api)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    private HttpClient Vendedor => api.Cliente(_construtora, Papeis.Vendedor);

    private async Task<(string Conta, ContaResposta Lida)> RotaSul()
    {
        var criada = await Vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul()).ComSucesso();

        return (criada.Headers.Location!.ToString(), await criada.Ler<ContaResposta>());
    }

    [Fact]
    public async Task Cria_a_conta_com_o_documento_formatado_e_o_primeiro_contato_de_principal()
    {
        var criada = await Vendedor.Criar("/api/contas", Exemplos.ContaDaRotaSul("11222333000181")).ComSucesso();
        var conta = await criada.Ler<ContaResposta>();

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Assert.Equal($"/api/contas/{conta.Id}", criada.Headers.Location?.ToString());
        Assert.Equal("\"1\"", criada.Headers.ETag?.Tag);
        Assert.Equal(("Logística Rota Sul Ltda.", Exemplos.CnpjDaMatriz), (conta.Nome, conta.Documento));

        var otavio = Assert.Single(conta.Contatos);
        Assert.Equal(otavio.Id, conta.Principal);
        Assert.Equal(
            new ContatoResposta(otavio.Id, "Otávio Reis", "Diretor de operações", "otavio.reis@rotasul.com.br", "(19) 3333-4444", PapelNaCompra.Decisor),
            otavio);
        Assert.Equivalent(conta, await Vendedor.LerComSucesso<ContaResposta>($"/api/contas/{conta.Id}"), strict: true);
    }

    [Fact]
    public async Task O_documento_de_outra_conta_e_409()
    {
        await RotaSul();

        var outra = await Vendedor.Criar("/api/contas", new { nome = "Rota Sul Transportes", documento = "11222333000181" });
        var problema = await outra.Problema();

        Assert.Equal(HttpStatusCode.Conflict, outra.StatusCode);
        Assert.EndsWith("#ja-existe", problema.Type, StringComparison.Ordinal);
        Assert.Equal("Já existe uma conta com o documento 11.222.333/0001-81: o documento identifica o cliente.", problema.Detail);
    }

    [Fact]
    public async Task Lista_em_ordem_de_nome_com_a_versao()
    {
        await Vendedor.Criar("/api/contas", new { nome = "Construtora Beta" }).ComSucesso();
        var alfa = await Vendedor.Criar("/api/contas", new { nome = "Construtora Alfa" }).IdCriado();
        await Vendedor.Enviar(HttpMethod.Patch, $"/api/contas/{alfa}", new { nome = "Alfa Engenharia" }, versao: 1).ComSucesso();

        var contas = await Vendedor.LerComSucesso<List<ResumoResposta>>("/api/contas");

        Assert.Equal([("Alfa Engenharia", 2), ("Construtora Beta", 1)], contas.Select(conta => (conta.Nome, conta.Versao)));
    }

    [Fact]
    public async Task O_documento_se_informa_uma_vez_e_nao_muda()
    {
        var conta = (await Vendedor.Criar("/api/contas", new { nome = "Rota Sul" }).ComSucesso()).Headers.Location!.ToString();

        var informado = await Vendedor.Enviar(HttpMethod.Patch, conta, new { documento = Exemplos.CnpjDaMatriz }, versao: 1).LerComSucesso<ContaResposta>();
        var trocado = await Vendedor.Enviar(HttpMethod.Patch, conta, new { documento = Exemplos.CnpjDaFilial }, versao: 2);

        Assert.Equal(Exemplos.CnpjDaMatriz, informado.Documento);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, trocado.StatusCode);
        Assert.Contains("o documento identifica a conta", (await trocado.Problema()).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alteracao_sem_nada_para_mudar_e_400()
    {
        var (conta, _) = await RotaSul();

        var resposta = await Vendedor.Enviar(HttpMethod.Patch, conta, new { }, versao: 1);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Os_contatos_entram_mudam_trocam_de_principal_e_saem()
    {
        var (conta, lida) = await RotaSul();
        var otavio = lida.Contatos.Single().Id;

        var comRita = await Vendedor
            .Enviar(HttpMethod.Post, $"{conta}/contatos", new { nome = "Rita Nunes", telefone = "+55 19 98888-7777", papel = "Financeiro" }, versao: 1)
            .LerComSucesso<ContaResposta>();
        var rita = comRita.Contatos.Single(contato => contato.Nome == "Rita Nunes").Id;

        var alterada = await Vendedor
            .Enviar(HttpMethod.Put, $"{conta}/contatos/{rita}", new { nome = "Rita Nunes", email = "rita.nunes@rotasul.com.br", cargo = "Gerente financeira", papel = "Financeiro" }, versao: 2)
            .LerComSucesso<ContaResposta>();
        var principalOcupado = await Vendedor.Enviar(HttpMethod.Delete, $"{conta}/contatos/{otavio}", versao: 3);
        await Vendedor.Enviar(HttpMethod.Put, $"{conta}/principal", new { contato = rita }, versao: 3).ComSucesso();
        var semOtavio = await Vendedor.Enviar(HttpMethod.Delete, $"{conta}/contatos/{otavio}", versao: 4).LerComSucesso<ContaResposta>();

        Assert.Equal(
            new ContatoResposta(rita, "Rita Nunes", "Gerente financeira", "rita.nunes@rotasul.com.br", null, PapelNaCompra.Financeiro),
            alterada.Contatos.Single(contato => contato.Id == rita));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, principalOcupado.StatusCode);
        Assert.Contains("escolha outro principal antes de remover", (await principalOcupado.Problema()).Detail, StringComparison.Ordinal);
        Assert.Equal(rita, Assert.Single(semOtavio.Contatos).Id);
        Assert.Equal(rita, semOtavio.Principal);
        Assert.Equal(otavio, comRita.Principal);
    }

    [Fact]
    public async Task O_e_mail_de_outro_contato_da_conta_e_422()
    {
        var (conta, _) = await RotaSul();

        var resposta = await Vendedor.Enviar(HttpMethod.Post, $"{conta}/contatos", new { nome = "Otávio", email = "OTAVIO.REIS@rotasul.com.br" }, versao: 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("O e-mail otavio.reis@rotasul.com.br já é do contato Otávio Reis.", (await resposta.Problema()).Detail);
    }

    [Theory]
    [InlineData("marina@", "não é um e-mail")]
    [InlineData(null, "precisa de um e-mail ou de um telefone")]
    public async Task Contato_sem_como_falar_com_ele_e_422(string? email, string motivo)
    {
        var (conta, _) = await RotaSul();

        var resposta = await Vendedor.Enviar(HttpMethod.Post, $"{conta}/contatos", new { nome = "Marina", email }, versao: 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains(motivo, (await resposta.Problema()).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_contato_do_caminho_que_nao_existe_e_404_e_o_do_corpo_e_422()
    {
        var (conta, _) = await RotaSul();
        var inventado = Guid.CreateVersion7();

        var noCaminho = await Vendedor.Enviar(HttpMethod.Delete, $"{conta}/contatos/{inventado}", versao: 1);
        var noCorpo = await Vendedor.Enviar(HttpMethod.Put, $"{conta}/principal", new { contato = inventado }, versao: 1);

        Assert.Equal(HttpStatusCode.NotFound, noCaminho.StatusCode);
        Assert.Equal($"A conta {conta.Split('/')[^1]} não tem o contato {inventado}.", (await noCaminho.Problema()).Detail);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noCorpo.StatusCode);
    }

    [Fact]
    public async Task A_gerencia_remove_a_conta_cadastrada_errado()
    {
        var (conta, _) = await RotaSul();

        var removida = await api.Cliente(_construtora, Papeis.Gerente).Enviar(HttpMethod.Delete, conta, versao: 1);

        Assert.Equal(HttpStatusCode.NoContent, removida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Vendedor.GetAsync(conta, Cancelamento)).StatusCode);
    }
}
