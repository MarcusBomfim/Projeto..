using System.Globalization;
using System.Net;
using CrmObras.Api.Recursos.Atividades;
using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Recursos.Funis;
using CrmObras.Api.Recursos.Historico;
using CrmObras.Api.Recursos.Leads;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Recursos.Propostas;
using CrmObras.Api.Recursos.Relatorios;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Api.Testes;

/*
 * O exemplo do README, de ponta a ponta, pela API — cada passo por quem tem
 * o papel para ele, cada um no dia em que acontece. O domínio já confere
 * essas frases e esses números; aqui se confere que eles atravessam o HTTP,
 * o JSON e o banco sem mudar uma vírgula.
 */
public sealed class ExemploDoReadmePelaApiTests(ApiDeTestes api)
{
    private static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    private readonly CenarioDeVenda _cenario = new(api);

    private static DateTimeOffset Em(int dia, int mes, int hora, int minuto = 0) => CenarioDeVenda.Em(dia, mes, hora, minuto);

    private static Guid Etapa(FunilResposta funil, string nome) => funil.Etapas.Single(etapa => etapa.Nome == nome).Id;

    private HttpClient Carlos => _cenario.Carlos;

    [Fact]
    public async Task Do_formulario_do_site_a_venda_ganha_com_as_frases_do_README()
    {
        // Segunda, 19/10: a construtora já atende a matriz; pelo site chega a Marina.
        _cenario.Relogio(Em(19, 10, 9));
        var matriz = await Carlos.Criar("/api/contas", new
        {
            nome = "Logística Rota Sul",
            documento = "11.222.333/0001-81",
            contatos = new[] { new { nome = "Otávio Reis", email = "otavio.reis@rotasul.com.br", telefone = "(11) 3333-4444", cargo = "Diretor de obras", papel = "Decisor" } },
        }).IdCriado();
        var marina = await Carlos.Criar("/api/leads", new
        {
            nome = "Marina Costa",
            origem = "Site",
            empresa = "Rota Sul Logística — Campinas",
            documento = "11.222.333/0002-62",
            email = "Marina.Costa@RotaSul.com.br",
            telefone = "+55 19 99876-5432",
            interesse = "Galpão de 1.200 m² em Jundiaí",
        }).LerComSucesso<LeadResposta>();
        var lead = $"/api/leads/{marina.Id}";
        Assert.Equal(("marina.costa@rotasul.com.br", "(19) 99876-5432"), (marina.Email, marina.Telefone));

        // Há verba e a obra é real, mas a Marina não decide, e ainda não há data.
        await Carlos.Enviar(HttpMethod.Put, $"{lead}/avaliacao", new { orcamento = "Sim", falaComQuemDecide = "Nao", necessidade = "Sim", prazo = "NaoSabido" }, versao: 1)
            .ComSucesso();
        var cedo = await Carlos.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 2);
        Assert.Equal("O lead Marina Costa ainda não se qualifica: falta falar com quem decide ou ter o prazo da obra.", (await cedo.Problema()).Detail);

        // Quarta, 21/10: a obra tem data, e o lead se qualifica. A busca de duplicados acha a matriz.
        _cenario.Relogio(Em(21, 10, 9));
        await Carlos.Enviar(HttpMethod.Put, $"{lead}/avaliacao", new { orcamento = "Sim", falaComQuemDecide = "Nao", necessidade = "Sim", prazo = "Sim" }, versao: 2)
            .ComSucesso();
        await Carlos.Enviar(HttpMethod.Post, $"{lead}/qualificacao", versao: 3).ComSucesso();
        var duplicado = Assert.Single(await Carlos.LerComSucesso<List<PossivelDuplicadoResposta>>($"{lead}/duplicados"));
        Assert.Equal(
            ["a mesma empresa: 11.222.333/0001-81 tem a raiz do CNPJ do lead", "Otávio Reis também tem e-mail em rotasul.com.br"],
            duplicado.Indicios.Select(indicio => indicio.Motivo));

        // A filial tem CNPJ próprio: a matriz não a recebe por engano, e a conta da filial nasce com a Marina.
        var naMatriz = await Carlos.Enviar(HttpMethod.Post, $"{lead}/conversao", new { conta = matriz }, versao: 4);
        Assert.Equal(
            "O lead é do cliente com o documento 11.222.333/0002-62, e a conta Logística Rota Sul tem o documento 11.222.333/0001-81: não são o mesmo cliente.",
            (await naMatriz.Problema()).Detail);
        var conversao = await Carlos.Enviar(HttpMethod.Post, $"{lead}/conversao", new { }, versao: 4).LerComSucesso<ConversaoResposta>();
        var filial = conversao.Conta;
        Assert.Equal(
            ("Rota Sul Logística — Campinas", "11.222.333/0002-62", "Marina Costa", SituacaoDoLead.Convertido),
            (filial.Nome, filial.Documento, filial.Contatos.Single(contato => contato.Id == filial.Principal).Nome, conversao.Lead.Situacao));

        // A venda nasce do lead, no funil padrão; a qualificação disse que a Marina não decide.
        var funil = await _cenario.FunilPadrao();
        var galpao = await Carlos.Criar("/api/oportunidades/do-lead", new { lead = marina.Id, funil = funil.Id }).LerComSucesso<OportunidadeResposta>();
        var venda = $"/api/oportunidades/{galpao.Id}";
        Assert.Equal(("Galpão de 1.200 m² em Jundiaí", Etapa(funil, "Primeiro contato")), (galpao.Titulo, galpao.Etapa));
        Assert.Null(galpao.QuemDecide);

        // Sexta, 23/10, a visita técnica; segunda, 26/10, o orçamento não sai sem quem decide.
        _cenario.Relogio(Em(23, 10, 9));
        await Carlos.Enviar(HttpMethod.Put, $"{venda}/etapa", new { etapa = Etapa(funil, "Visita técnica") }, versao: 1).ComSucesso();
        _cenario.Relogio(Em(26, 10, 9));
        var semQuemDecide = await Carlos.Enviar(HttpMethod.Put, $"{venda}/etapa", new { etapa = Etapa(funil, "Orçamento em elaboração") }, versao: 2);
        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Orçamento em elaboração, falta quem decide.",
            (await semQuemDecide.Problema()).Detail);

        // Na visita, a Marina apresentou o Sérgio, que decide.
        var comSergio = await Carlos.Enviar(HttpMethod.Post, $"/api/contas/{filial.Id}/contatos", new { nome = "Sérgio Lima", email = "sergio.lima@rotasul.com.br", papel = "Decisor" }, versao: 1)
            .LerComSucesso<ContaResposta>();
        var sergio = comSergio.Contatos.Single(contato => contato.Nome == "Sérgio Lima").Id;
        await Carlos.Enviar(HttpMethod.Patch, venda, new { quemDecide = sergio }, versao: 2).ComSucesso();
        await Carlos.Enviar(HttpMethod.Put, $"{venda}/etapa", new { etapa = Etapa(funil, "Orçamento em elaboração") }, versao: 3).ComSucesso();

        // Quinta, 05/11: pular a proposta não pula o que ela pede. Com valor e previsão, a venda e a proposta vão juntas.
        _cenario.Relogio(Em(5, 11, 9));
        var pulando = await Carlos.Enviar(HttpMethod.Put, $"{venda}/etapa", new { etapa = Etapa(funil, "Negociação") }, versao: 4);
        Assert.Equal(
            "Para a oportunidade Galpão de 1.200 m² em Jundiaí entrar em Negociação, falta o valor estimado e a previsão de fechamento.",
            (await pulando.Problema()).Detail);
        await Carlos.Enviar(HttpMethod.Patch, venda, new { valorEstimado = 2_400_000m, previsaoDeFechamento = "2026-12-15" }, versao: 4).ComSucesso();
        await Carlos.Enviar(HttpMethod.Put, $"{venda}/etapa", new { etapa = Etapa(funil, "Proposta enviada") }, versao: 5).ComSucesso();

        object[] itens =
        [
            new { descricao = "Piso industrial em concreto armado, fck 30 MPa, 15 cm", quantidade = 1_200m, unidade = "m²", precoUnitario = 185.50m },
            new { descricao = "Estrutura metálica do galpão", quantidade = 1m, unidade = "vb", precoUnitario = 1_450_000m },
            new { descricao = "Cobertura em telha termoacústica", quantidade = 1_350m, unidade = "m²", precoUnitario = 312.40m },
        ];
        const string Condicoes = "30% na assinatura e o resto por medição";
        var criada = await Carlos.Criar("/api/propostas", new { oportunidade = galpao.Id, versao = new { itens, desconto = 0m, validaAte = "2026-12-31", condicoesDePagamento = Condicoes } })
            .ComSucesso();
        var proposta = criada.Headers.Location!.ToString();
        Assert.Equal(2_094_340.00m, (await criada.Ler<PropostaResposta>()).Versoes[0].Total);
        await Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 1).ComSucesso();

        // Quinta, 26/11, três semanas depois: a venda parada no quadro, e sem próximo passo na agenda.
        _cenario.Relogio(Em(26, 11, 9));
        var coluna = (await _cenario.Leitor.LerComSucesso<QuadroResposta>($"/api/funis/{funil.Id}/quadro")).Colunas[3];
        Assert.Equal(("Proposta enviada", 2_400_000.00m, 1_440_000.00m), (coluna.Nome, coluna.Total, coluna.Ponderado));
        Assert.True(Assert.Single(coluna.Oportunidades).Parada);
        var passagens = (await Carlos.LerComSucesso<OportunidadeResposta>(venda)).Passagens;
        Assert.Equal([2, 3, 10, 21], passagens.Select(passagem => ((passagem.SaiuEm ?? Em(26, 11, 9)) - passagem.EntrouEm).Days));
        Assert.Equal([galpao.Id], (await Carlos.LerComSucesso<AgendaResposta>("/api/agenda")).SemProximoPasso.Select(semPasso => semPasso.Id));

        // O Carlos marca a ligação para sexta, às 10h; às 14h, ela ainda não foi feita.
        var retorno = await Carlos.Criar("/api/atividades", new { conta = filial.Id, tipo = "Ligacao", assunto = "Retorno da proposta", previstaPara = Em(27, 11, 10), com = sergio, oportunidade = galpao.Id })
            .IdCriado();
        _cenario.Relogio(Em(27, 11, 14));
        var agenda = await Carlos.LerComSucesso<AgendaResposta>("/api/agenda");
        Assert.Equal([retorno], agenda.Atrasadas.Select(atividade => atividade.Id));
        Assert.Empty(agenda.SemProximoPasso);

        // O Sérgio acha caro, e pede 12%: passa da alçada do vendedor — e da do gerente.
        _cenario.Relogio(Em(27, 11, 14, 30));
        await Carlos.Enviar(HttpMethod.Post, $"/api/atividades/{retorno}/conclusao", new { resultado = "Achou caro: pede 12% de desconto para fechar este ano" }, versao: 1)
            .ComSucesso();
        _cenario.Relogio(Em(30, 11, 9));
        await Carlos.Enviar(HttpMethod.Post, $"{proposta}/versoes", versao: 2).ComSucesso();
        await Carlos.Enviar(HttpMethod.Put, $"{proposta}/rascunho", new { itens, desconto = 12m, validaAte = "2026-12-31", condicoesDePagamento = Condicoes }, versao: 3)
            .ComSucesso();
        var semAprovacao = await Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 4);
        var daGerente = await _cenario.Helena.Enviar(HttpMethod.Post, $"{proposta}/aprovacao", versao: 4);
        Assert.Equal("O desconto de 12% passa da alçada do vendedor, de 5%: precisa da aprovação do diretor.", (await semAprovacao.Problema()).Detail);
        Assert.Equal("O gerente aprova até 10%: o desconto de 12% é do diretor.", (await daGerente.Problema()).Detail);

        _cenario.Relogio(Em(30, 11, 10));
        await _cenario.Roberto.Enviar(HttpMethod.Post, $"{proposta}/aprovacao", versao: 4).ComSucesso();
        _cenario.Relogio(Em(30, 11, 11));
        var segunda = (await Carlos.Enviar(HttpMethod.Post, $"{proposta}/envio", versao: 5).LerComSucesso<PropostaResposta>()).Versoes[1];
        Assert.Equal((2_094_340.00m, 251_320.80m, 1_843_019.20m), (segunda.Subtotal, segunda.ValorDoDesconto, segunda.Total));

        // Segunda, 07/12: o Sérgio aceita, e a venda é ganha pelo total da versão 2.
        _cenario.Relogio(Em(7, 12, 11));
        var aceite = await Carlos.Enviar(HttpMethod.Post, $"{proposta}/aceite", versao: 6).LerComSucesso<AceiteResposta>();
        Assert.Equal((SituacaoDaOportunidade.Ganha, 1_843_019.20m), (aceite.Oportunidade.Situacao, aceite.Oportunidade.ValorFechado));

        // E a linha do tempo da filial conta a história para quem assumir a conta.
        var linha = await _cenario.Leitor.LerComSucesso<List<AcontecimentoResposta>>($"/api/contas/{filial.Id}/linha-do-tempo");
        Assert.Equal(
            [
                "07/12 Venda ganha: Galpão de 1.200 m² em Jundiaí, por R$ 1.843.019,20",
                "07/12 Proposta aceita: versão 2, R$ 1.843.019,20",
                "30/11 Proposta enviada: versão 2, R$ 1.843.019,20, com 12% de desconto",
                "27/11 Ligação: Retorno da proposta — Achou caro: pede 12% de desconto para fechar este ano",
                "05/11 Galpão de 1.200 m² em Jundiaí: entrou em Proposta enviada",
                "05/11 Proposta enviada: versão 1, R$ 2.094.340,00",
                "26/10 Galpão de 1.200 m² em Jundiaí: entrou em Orçamento em elaboração",
                "23/10 Galpão de 1.200 m² em Jundiaí: entrou em Visita técnica",
                "21/10 Venda aberta: Galpão de 1.200 m² em Jundiaí, em Primeiro contato",
            ],
            linha.Select(acontecimento => $"{acontecimento.Em.ToOffset(Brasilia).ToString("dd'/'MM", CultureInfo.InvariantCulture)} {acontecimento.Descricao}"));
    }

    [Fact]
    public async Task Os_relatorios_do_trimestre_com_os_numeros_do_README()
    {
        var (filial, _, sergio) = await _cenario.Filial();
        var funil = await _cenario.FunilPadrao();

        async Task<Guid> Venda(string titulo, DateTimeOffset criadaEm, decimal valor, string previsao, params (string Etapa, DateTimeOffset Em)[] passos)
        {
            _cenario.Relogio(criadaEm);
            var venda = await Carlos.Criar("/api/oportunidades", new { conta = filial, funil = funil.Id, titulo, valorEstimado = valor, previsaoDeFechamento = previsao, quemDecide = sergio })
                .IdCriado();
            var versao = 1;

            foreach (var (etapa, em) in passos)
            {
                _cenario.Relogio(em);
                await Carlos.Enviar(HttpMethod.Put, $"/api/oportunidades/{venda}/etapa", new { etapa = Etapa(funil, etapa) }, versao++).ComSucesso();
            }

            return venda;
        }

        async Task Fechar(Guid venda, int versao, DateTimeOffset em, string caminho, object corpo)
        {
            _cenario.Relogio(em);
            await Carlos.Enviar(HttpMethod.Post, $"/api/oportunidades/{venda}/{caminho}", corpo, versao).ComSucesso();
        }

        // A carteira da filial no último trimestre: o galpão, ganho em dezembro, e mais cinco vendas.
        var galpao = await Venda("Galpão de 1.200 m² em Jundiaí", Em(21, 10, 9), 2_400_000m, "2026-12-15", ("Visita técnica", Em(23, 10, 9)), ("Orçamento em elaboração", Em(26, 10, 9)), ("Proposta enviada", Em(5, 11, 9)));
        await Fechar(galpao, 4, Em(7, 12, 11), "ganho", new { valorFechado = 1_843_019.20m });
        var escritorio = await Venda("Escritório de Campinas", Em(3, 11, 9), 600_000m, "2027-01-29", ("Visita técnica", Em(10, 11, 9)), ("Orçamento em elaboração", Em(17, 11, 9)), ("Proposta enviada", Em(10, 12, 9)));
        var doca = await Venda("Doca de Sumaré", Em(10, 11, 9), 300_000m, "2026-12-18", ("Visita técnica", Em(12, 11, 9)), ("Orçamento em elaboração", Em(20, 11, 9)));
        await Fechar(doca, 3, Em(15, 12, 9), "perda", new { motivo = "Preco", concorrente = "Construtora Beta" });
        var portaria = await Venda("Portaria de Jundiaí", Em(1, 12, 9), 150_000m, "2027-02-26");
        var refeitorio = await Venda("Reforma do refeitório", Em(14, 10, 9), 80_000m, "2026-11-30", ("Visita técnica", Em(16, 10, 9)));
        await Fechar(refeitorio, 2, Em(30, 11, 9), "perda", new { motivo = "SemResposta" });
        var estacionamento = await Venda("Estacionamento", Em(20, 10, 9), 400_000m, "2026-12-18", ("Negociação", Em(5, 11, 9)));

        // Na primeira segunda de janeiro, a previsão dos próximos três meses.
        _cenario.Relogio(new DateTimeOffset(2027, 1, 4, 9, 0, 0, Brasilia));
        var previsao = await _cenario.Leitor.LerComSucesso<PrevisaoResposta>("/api/relatorios/previsao?primeiro=2027-01&meses=3");

        Assert.Equal(
            [("2027-01", 600_000.00m, 360_000.00m), ("2027-02", 150_000.00m, 15_000.00m), ("2027-03", 0m, 0m)],
            previsao.Meses.Select(mes => (mes.Mes, mes.EmAberto, mes.Ponderado)));
        Assert.Equal([escritorio], previsao.Meses[0].Abertas.Select(aberta => aberta.Id));
        Assert.Equal([portaria], previsao.Meses[1].Abertas.Select(aberta => aberta.Id));
        Assert.Equal([estacionamento], previsao.Vencidas.Select(vencida => vencida.Id));

        // O trimestre: a conversão, o ciclo e as perdas.
        var trimestre = $"funil={funil.Id}&de=2026-10-01&ate=2026-12-31";
        var conversao = await _cenario.Leitor.LerComSucesso<ConversaoPorEtapaResposta>($"/api/relatorios/conversao?{trimestre}");
        Assert.Equal(
            [
                ("Primeiro contato", 6, 1, 2, (decimal?)33.3m),
                ("Visita técnica", 5, 1, 2, 33.3m),
                ("Orçamento em elaboração", 4, 1, 1, 50m),
                ("Proposta enviada", 3, 1, 0, 100m),
                ("Negociação", 2, 1, 0, 100m),
            ],
            conversao.Etapas.Select(etapa => (etapa.Nome, etapa.Chegaram, etapa.Ganhas, etapa.Perdidas, etapa.TaxaDeGanho)));

        var ciclo = await _cenario.Leitor.LerComSucesso<CicloDeVendaResposta>($"/api/relatorios/ciclo?{trimestre}");
        Assert.Equal((1, 47.1m), (ciclo.Ganhas, ciclo.MedianaEmDias));
        Assert.Equal([2m, 3m, 10m, 32.1m, (decimal?)null], ciclo.Etapas.Select(etapa => etapa.MedianaEmDias));

        var perdas = await _cenario.Leitor.LerComSucesso<MotivosDePerdaResposta>($"/api/relatorios/perdas?{trimestre}");
        Assert.Equal((2, 380_000.00m), (perdas.Perdidas, perdas.Valor));
        Assert.Equal(
            [(MotivoDaPerda.Preco, 300_000.00m), (MotivoDaPerda.SemResposta, 80_000.00m)],
            perdas.PorMotivo.Select(motivo => (motivo.Motivo, motivo.Valor)));
        Assert.Equal("Construtora Beta", Assert.Single(perdas.PorConcorrente).Concorrente);
        Assert.Equal(["Visita técnica", "Orçamento em elaboração"], perdas.PorEtapa.Select(etapa => etapa.Nome));
        Assert.Equal(HttpStatusCode.OK, (await _cenario.Leitor.GetAsync($"/api/funis/{funil.Id}/quadro", TestContext.Current.CancellationToken)).StatusCode);
    }
}
