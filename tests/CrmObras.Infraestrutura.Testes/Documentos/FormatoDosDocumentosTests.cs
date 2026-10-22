using System.Text.Json.Nodes;
using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Dominio.Testes.Leads;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Testes.Documentos;

/*
 * O formato gravado de cada agregado, escrito por extenso. Renomear uma
 * propriedade do domínio, ou do documento, muda o que está no banco — e este
 * teste é onde isso aparece, antes de um documento gravado deixar de ser
 * lido (ADR-0011). A comparação é pela estrutura do JSON: o jsonb não guarda
 * a ordem das chaves, nem os escapes.
 */
public sealed class FormatoDosDocumentosTests
{
    private static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_conta()
    {
        var conta = new Conta("Rota Sul", Cnpj.De("11.222.333/0001-81"));
        var marina = conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina@rotasul.com.br"), Telefone.De("(19) 99876-5432"), "Compradora", PapelNaCompra.Influenciador));
        var sergio = conta.AdicionarContato(new DadosDoContato("Sérgio Lima", Telefone: Telefone.De("(11) 3333-4444"), Papel: PapelNaCompra.Decisor));

        Igual(
            $$"""
            {
              "nome": "Rota Sul",
              "documento": "11222333000181",
              "principal": "{{marina.Id}}",
              "contatos": [
                { "id": "{{marina.Id}}", "nome": "Marina Costa", "email": "marina@rotasul.com.br", "telefone": "+5519998765432", "cargo": "Compradora", "papel": "Influenciador" },
                { "id": "{{sergio.Id}}", "nome": "Sérgio Lima", "email": null, "telefone": "+551133334444", "cargo": null, "papel": "Decisor" }
              ]
            }
            """,
            RegistroDeConta.De(conta, 1, Agora).Documento);
    }

    [Fact]
    public void O_lead()
    {
        var lead = LeadsDeTeste.Qualificado(LeadsDeTeste.DoGalpao(Cnpj.De("11.222.333/0001-81")));
        var conta = lead.ConverterEmContaNova([], Segunda.AddDays(4));

        Igual(
            $$"""
            {
              "nome": "Marina Costa",
              "empresa": "Logística Rota Sul Ltda",
              "documento": "11222333000181",
              "email": "marina.costa@rotasul.com.br",
              "telefone": "+5511998765432",
              "interesse": "Galpão de 1.200 m² em Jundiaí, para começar em março",
              "origem": "Site",
              "detalheDaOrigem": null,
              "chegouEm": "2026-10-19T09:00:00-03:00",
              "situacao": "Convertido",
              "avaliacao": { "orcamento": "Sim", "falaComQuemDecide": "Sim", "necessidade": "Sim", "prazo": "NaoSabido" },
              "qualificadoEm": "2026-10-21T09:00:00-03:00",
              "motivoDoDescarte": null,
              "observacaoDoDescarte": null,
              "descartadoEm": null,
              "conta": "{{conta.Id}}",
              "contato": "{{conta.Principal?.Id}}",
              "convertidoEm": "2026-10-23T09:00:00-03:00"
            }
            """,
            RegistroDeLead.De(lead, 1, Agora).Documento);
    }

    [Fact]
    public void O_funil()
    {
        var funil = new Funil(
            "Obras públicas",
            [new("Edital publicado", 5, 30), new("Habilitação", 30), new("Proposta", 60, Requisitos: Requisitos.QuemDecide | Requisitos.ValorEstimado)]);
        var habilitacao = funil.Abertas[1];
        funil.ArquivarEtapa(habilitacao.Id);
        funil.RenomearEtapa(funil.Ganha.Id, "Contrato assinado");

        Igual(
            $$"""
            {
              "nome": "Obras públicas",
              "abertas": [
                { "id": "{{funil.Abertas[0].Id}}", "nome": "Edital publicado", "probabilidade": 5, "diasEsperados": 30, "requisitos": "Nenhum" },
                { "id": "{{funil.Abertas[1].Id}}", "nome": "Proposta", "probabilidade": 60, "diasEsperados": null, "requisitos": "ValorEstimado, QuemDecide" }
              ],
              "arquivadas": [
                { "id": "{{habilitacao.Id}}", "nome": "Habilitação", "probabilidade": 30, "diasEsperados": null, "requisitos": "Nenhum" }
              ],
              "ganha": { "id": "{{funil.Ganha.Id}}", "nome": "Contrato assinado" },
              "perdida": { "id": "{{funil.Perdida.Id}}", "nome": "Perdida" }
            }
            """,
            RegistroDeFunil.De(funil, 1, Agora).Documento);
    }

    [Fact]
    public void A_oportunidade()
    {
        var funil = new Funil("Vendas", [new("Contato", 10), new("Proposta", 60)]);
        var lead = LeadId.Novo();
        var venda = new Oportunidade(ContaId.Novo(), "Galpão", funil, "carlos.souza", Segunda, lead);
        venda.Estimar(Dinheiro.Reais(2_400_000.5m));
        venda.PreverFechamento(new DateOnly(2026, 12, 15));
        venda.MoverPara(funil, funil.Abertas[1].Id, Segunda.AddDays(5));
        venda.Perder(funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, "Construtora Beta"), Segunda.AddDays(20));

        Igual(
            $$"""
            {
              "titulo": "Galpão",
              "responsavel": "carlos.souza",
              "criadaEm": "2026-10-19T09:00:00-03:00",
              "lead": "{{lead}}",
              "situacao": "Perdida",
              "passagens": [
                { "etapa": "{{funil.Abertas[0].Id}}", "entrouEm": "2026-10-19T09:00:00-03:00", "saiuEm": "2026-10-24T09:00:00-03:00" },
                { "etapa": "{{funil.Abertas[1].Id}}", "entrouEm": "2026-10-24T09:00:00-03:00", "saiuEm": "2026-11-08T09:00:00-03:00" },
                { "etapa": "{{funil.Perdida.Id}}", "entrouEm": "2026-11-08T09:00:00-03:00", "saiuEm": null }
              ],
              "valorEstimado": 2400000.5,
              "previsaoDeFechamento": "2026-12-15",
              "quemDecide": null,
              "valorFechado": null,
              "perda": { "motivo": "ConcorrenteEscolhido", "concorrente": "Construtora Beta", "observacao": null },
              "fechadaEm": "2026-11-08T09:00:00-03:00"
            }
            """,
            RegistroDeOportunidade.De(venda, 1, Agora).Documento);
    }

    [Fact]
    public void A_atividade()
    {
        var conta = new Conta("Rota Sul");
        var sergio = conta.AdicionarContato(new DadosDoContato("Sérgio Lima", Email.De("sergio@rotasul.com.br")));
        var atividade = new Atividade(conta, new DadosDaAtividade(TipoDeAtividade.Ligacao, "Retorno da proposta", Segunda.AddDays(3), sergio.Id), "carlos.souza", Segunda);
        atividade.Reagendar(Segunda.AddDays(4), Segunda.AddDays(1));
        atividade.Concluir("Pediu 12%", Segunda.AddDays(4));

        Igual(
            $$"""
            {
              "tipo": "Ligacao",
              "assunto": "Retorno da proposta",
              "previstaPara": "2026-10-23T09:00:00-03:00",
              "com": "{{sergio.Id}}",
              "detalhes": null,
              "responsavel": "carlos.souza",
              "criadaEm": "2026-10-19T09:00:00-03:00",
              "adiamentos": 1,
              "situacao": "Concluida",
              "resultado": "Pediu 12%",
              "motivoDoCancelamento": null,
              "encerradaEm": "2026-10-23T09:00:00-03:00"
            }
            """,
            RegistroDeAtividade.De(atividade, 1, Agora).Documento);
    }

    [Fact]
    public void A_proposta()
    {
        var funil = new Funil("Vendas", [new("Contato", 10)]);
        var venda = new Oportunidade(ContaId.Novo(), "Galpão", funil, "carlos.souza", Segunda);
        var proposta = new Proposta(venda, new DadosDaVersao([new("Piso polido", 1_200m, "m²", Dinheiro.Reais(185.5m))], 7.5m, new DateOnly(2026, 11, 30), "À vista"), Segunda);
        proposta.AprovarDesconto(new Comercial("helena.martins", Cargo.Gerente), Alcadas.Padrao, Segunda.AddHours(1));
        proposta.Enviar(new Comercial("carlos.souza", Cargo.Vendedor), Alcadas.Padrao, Segunda.AddHours(2));
        proposta.Aceitar(Segunda.AddDays(3));

        Igual(
            """
            {
              "criadaEm": "2026-10-19T09:00:00-03:00",
              "versoes": [
                {
                  "numero": 1,
                  "itens": [ { "descricao": "Piso polido", "quantidade": 1200, "unidade": "m²", "precoUnitario": 185.5 } ],
                  "desconto": 7.5,
                  "validaAte": "2026-11-30",
                  "condicoesDePagamento": "À vista",
                  "situacao": "Aceita",
                  "aprovacao": { "aprovador": { "login": "helena.martins", "cargo": "Gerente" }, "em": "2026-10-19T10:00:00-03:00" },
                  "enviadaPor": { "login": "carlos.souza", "cargo": "Vendedor" },
                  "enviadaEm": "2026-10-19T11:00:00-03:00",
                  "respondidaEm": "2026-10-22T09:00:00-03:00",
                  "motivoDaRecusa": null
                }
              ]
            }
            """,
            RegistroDeProposta.De(proposta, 1, Agora).Documento);
    }

    private static void Igual(string esperado, string gravado) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(esperado), JsonNode.Parse(gravado)),
            $"O documento gravado não é o formato esperado:{Environment.NewLine}{gravado}");
}
