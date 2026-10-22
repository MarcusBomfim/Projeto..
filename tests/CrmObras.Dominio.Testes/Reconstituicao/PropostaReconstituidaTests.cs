using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Propostas;
using static CrmObras.Dominio.Testes.Propostas.PropostasDeTeste;

namespace CrmObras.Dominio.Testes.Reconstituicao;

public sealed class PropostaReconstituidaTests
{
    private static readonly DateTimeOffset Terca = Segunda.AddDays(1);

    [Fact]
    public void Volta_com_cada_versao_como_estava()
    {
        var proposta = DoGalpao(Venda());
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca);
        proposta.Recusar("Caro", Terca.AddDays(1));
        proposta.NovaVersao();
        proposta.Redigir(DoGalpao(8m));
        proposta.AprovarDesconto(Helena, Alcadas.Padrao, Terca.AddDays(2));
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca.AddDays(2));
        proposta.NovaVersao();
        proposta.Redigir(DoGalpao(4m) with { Itens = [Piso, Estrutura] });
        proposta.Enviar(Carlos, Alcadas.Padrao, Terca.AddDays(3));
        proposta.NovaVersao();

        var refeita = Refazer(proposta);

        Assert.Equal((proposta.Id, proposta.Oportunidade, proposta.Conta, proposta.CriadaEm), (refeita.Id, refeita.Oportunidade, refeita.Conta, refeita.CriadaEm));
        Assert.Equal(Descrever(proposta), Descrever(refeita));
        Assert.Equal(
            [SituacaoDaVersao.Recusada, SituacaoDaVersao.Substituida, SituacaoDaVersao.Enviada, SituacaoDaVersao.Rascunho],
            refeita.Versoes.Select(versao => versao.Situacao));
        Assert.Same(refeita.Versoes[2], refeita.Vigente);
    }

    [Fact]
    public void A_aceita_volta_aceita_e_fechada()
    {
        var proposta = Enviada();
        proposta.Aceitar(Terca.AddDays(5));

        var refeita = Refazer(proposta);

        Assert.Equal(Descrever(proposta), Descrever(refeita));
        Assert.Same(refeita.Versoes[0], refeita.Aceita);
    }

    [Fact]
    public void O_conteudo_passa_pelas_regras_de_quem_redige()
    {
        var proposta = DoGalpao(Venda());
        var estado = proposta.Atual.Estado;

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(proposta, [estado with { Conteudo = estado.Conteudo with { Itens = [Piso with { Descricao = "" }] } }]));

        Assert.Equal("Item 1: falta a descrição.", erro.Message);
    }

    private static readonly Dictionary<string, (Func<Proposta, IReadOnlyList<EstadoDaVersao>> Estragar, string Problema)> Estragos = new()
    {
        ["sem versão"] = (_ => [], "nenhuma versão"),
        ["com a numeração pulando"] = (proposta => [Estados(proposta)[0], Estados(proposta)[1] with { Numero = 3 }], "a numeração não é 1, 2, 3"),
        ["com rascunho no meio"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Rascunho, EnviadaPor = null, EnviadaEm = null }, Estados(proposta)[1] with { Situacao = SituacaoDaVersao.Enviada, EnviadaPor = Carlos, EnviadaEm = Terca }], "rascunho fora da última versão"),
        ["com dois rascunhos"] = (proposta => [.. Estados(proposta).Select(versao => versao with { Situacao = SituacaoDaVersao.Rascunho, EnviadaPor = null, EnviadaEm = null })], "rascunho fora da última versão"),
        ["com duas com o cliente"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Enviada }, Estados(proposta)[1] with { Situacao = SituacaoDaVersao.Enviada, EnviadaPor = Carlos, EnviadaEm = Terca }], "versão com o cliente, ou aceita, antes da última enviada"),
        ["com a última substituída"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Substituida }], "versão com o cliente, ou aceita, antes da última enviada"),
        ["com rascunho depois da aceita"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Aceita, RespondidaEm = Terca }, Estados(proposta)[1]], "rascunho depois da aceita"),
        ["enviada sem quem enviou"] = (proposta => [Estados(proposta)[0] with { EnviadaPor = null }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["rascunho com envio"] = (proposta => [Estados(proposta)[0], Estados(proposta)[1] with { EnviadaEm = Terca }], "a versão 2 não tem o envio ou a resposta que a situação dela pede"),
        ["enviada com resposta"] = (proposta => [Estados(proposta)[0] with { RespondidaEm = Terca }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["aceita sem resposta"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Aceita }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["respondida antes de enviada"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Aceita, RespondidaEm = Segunda }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["recusada sem motivo"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Recusada, RespondidaEm = Terca }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["recusada com espaço no motivo"] = (proposta => [Estados(proposta)[0] with { Situacao = SituacaoDaVersao.Recusada, RespondidaEm = Terca, MotivoDaRecusa = "Caro " }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
        ["aprovada sem desconto"] = (proposta => [Estados(proposta)[0] with { Aprovacao = new AprovacaoDeDesconto(Helena, Segunda) }], "a versão 1 não tem o envio ou a resposta que a situação dela pede"),
    };

    public static TheoryData<string> Estragados => [.. Estragos.Keys];

    [Theory]
    [MemberData(nameof(Estragados))]
    public void As_versoes_que_as_regras_nao_produzem_sao_recusadas(string caso)
    {
        var (estragar, problema) = Estragos[caso];
        var proposta = Enviada();
        proposta.NovaVersao();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Refazer(proposta, estragar(proposta)));

        Assert.Equal($"A proposta {proposta.Id} está gravada com versões que as regras não produzem: {problema}.", erro.Message);
    }

    [Fact]
    public void A_recusada_aceita_o_motivo_no_limite()
    {
        var proposta = Enviada();
        proposta.Recusar(new string('m', 1000), Terca.AddDays(1));

        Assert.Equal(1000, Refazer(proposta).Versoes[0].MotivoDaRecusa?.Length);
    }

    private static Proposta Refazer(Proposta proposta, IReadOnlyList<EstadoDaVersao>? versoes = null) =>
        Proposta.Reconstituir(proposta.Id, proposta.Oportunidade, proposta.Conta, proposta.CriadaEm, versoes ?? Estados(proposta));

    private static List<EstadoDaVersao> Estados(Proposta proposta) => [.. proposta.Versoes.Select(versao => versao.Estado)];

    private static string[] Descrever(Proposta proposta) =>
        [
            .. proposta.Versoes.Select(versao =>
                $"{versao.Numero} {versao.Situacao} {string.Join("; ", versao.Itens)} {versao.Desconto} {versao.ValidaAte} {versao.CondicoesDePagamento} "
                + $"{versao.Aprovacao} {versao.EnviadaPor} {versao.EnviadaEm} {versao.RespondidaEm} {versao.MotivoDaRecusa} {versao.Total}"),
        ];
}
