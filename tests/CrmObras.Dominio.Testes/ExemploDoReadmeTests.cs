using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;

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
        var matriz = Matriz();

        // Pelo site chega a Marina, da filial de Campinas, pedindo um galpão.
        var lead = LeadDaMarina();
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
    public void Do_lead_a_proposta_no_funil()
    {
        // A Marina, qualificada, virou a conta da filial.
        var matriz = Matriz();
        var lead = LeadDaMarina();
        lead.Avaliar(new Avaliacao(Resposta.Sim, Resposta.Nao, Resposta.Sim, Resposta.Sim));
        lead.Qualificar(Segunda.AddDays(2));
        var filial = lead.ConverterEmContaNova(BuscaDeDuplicados.Do(lead, [matriz]), Segunda.AddDays(2));

        // A venda nasce do lead, no funil padrão da construtora.
        var funil = Funil.PadraoDaConstrutora();
        Assert.Equal(
            "Primeiro contato 10% · Visita técnica 20% · Orçamento em elaboração 40% · Proposta enviada 60% · Negociação 80%",
            string.Join(" · ", funil.Abertas.Select(etapa => $"{etapa} {etapa.Probabilidade}%")));
        var visita = funil.Abertas[1].Id;
        var orcamento = funil.Abertas[2].Id;
        var proposta = funil.Abertas[3].Id;
        var negociacao = funil.Abertas[4].Id;

        var galpao = AberturaPeloLead.Abrir(lead, filial, funil, "carlos.souza", Segunda.AddDays(2));
        Assert.Equal(("Galpão de 1.200 m² em Jundiaí", "Primeiro contato"), (galpao.Titulo, funil.Etapa(galpao.Etapa).Nome));

        // A Marina não decide: a visita técnica acontece, mas o orçamento não sai sem quem decide.
        Assert.Null(galpao.QuemDecide);
        galpao.MoverPara(funil, visita, Segunda.AddDays(4));
        var semQuemDecide = Assert.Throws<ExcecaoDeDominio>(() => galpao.MoverPara(funil, orcamento, Segunda.AddDays(7)));
        Assert.Equal("Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Orçamento em elaboração, falta quem decide.", semQuemDecide.Message);

        // Na visita, a Marina apresentou o Sérgio, gerente da filial, que decide.
        var sergio = filial.AdicionarContato(new DadosDoContato("Sérgio Lima", Email.De("sergio.lima@rotasul.com.br"), Papel: PapelNaCompra.Decisor));
        galpao.DefinirQuemDecide(filial, sergio.Id);
        galpao.MoverPara(funil, orcamento, Segunda.AddDays(7));

        // Pular etapa não pula a exigência: a negociação pede o que a proposta pede.
        var pulando = Assert.Throws<ExcecaoDeDominio>(() => galpao.MoverPara(funil, negociacao, Segunda.AddDays(17)));
        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Negociação, falta o valor estimado e a previsão de fechamento.",
            pulando.Message);

        galpao.Estimar(Dinheiro.Reais(2_400_000m));
        galpao.PreverFechamento(new DateOnly(2026, 12, 15));
        galpao.MoverPara(funil, proposta, Segunda.AddDays(17));

        // Três semanas depois, a proposta continua sem resposta: o quadro mostra a venda parada.
        var tresSemanasDepois = Segunda.AddDays(17 + 21);
        var coluna = QuadroDoFunil.Do(funil, [galpao], tresSemanasDepois).Colunas[3];
        Assert.Equal(
            ("Proposta enviada", "R$ 2.400.000,00", "R$ 1.440.000,00", (int?)15),
            (coluna.Etapa.Nome, coluna.Total.ToString(), coluna.Ponderado.ToString(), coluna.Etapa.DiasEsperados));
        Assert.Equal([galpao], coluna.Paradas);
        Assert.Equal(
            "Primeiro contato 2 dias · Visita técnica 3 dias · Orçamento em elaboração 10 dias · Proposta enviada 21 dias",
            string.Join(" · ", galpao.TempoEmCadaEtapa(tresSemanasDepois).Select(tempo => $"{funil.Etapa(tempo.Etapa)} {tempo.Tempo.Days} dias")));
    }

    [Fact]
    public void O_cnpj_alfanumerico_e_conferido_como_o_numerico()
    {
        Assert.Equal("12.ABC.345/01DE-35", Documento.De("12abc34501de35").Formatado);

        var errado = Assert.Throws<ExcecaoDeDominio>(() => Documento.De("12.ABC.345/01DE-53"));
        Assert.Equal("O CNPJ 12.ABC.345/01DE-53 não confere: os dígitos verificadores não batem com o número.", errado.Message);
    }

    private static Conta Matriz()
    {
        var matriz = new Conta("Logística Rota Sul", Documento.De("11.222.333/0001-81"));
        matriz.AdicionarContato(new DadosDoContato(
            "Otávio Reis",
            Email.De("otavio.reis@rotasul.com.br"),
            Telefone.De("(11) 3333-4444"),
            "Diretor de obras",
            PapelNaCompra.Decisor));

        return matriz;
    }

    private static Lead LeadDaMarina() => new(
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
}
