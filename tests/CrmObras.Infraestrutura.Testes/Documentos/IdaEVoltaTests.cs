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
using CrmObras.Dominio.Testes.Oportunidades;
using CrmObras.Dominio.Testes.Propostas;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Testes.Documentos;

/*
 * Cada agregado vira registro — documento e colunas — e volta igual, sem
 * banco: o que se compara é tudo o que o agregado mostra. O que o banco faz
 * com o documento, os testes de persistência conferem.
 */
public sealed class IdaEVoltaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_conta_volta_com_os_contatos_e_o_principal()
    {
        var conta = new Conta("Logística Rota Sul", Cnpj.De("12.ABC.345/01DE-35"));
        conta.AdicionarContato(new DadosDoContato("Marina Costa", Email.De("marina@rotasul.com.br"), Telefone.De("(19) 99876-5432"), "Compradora", PapelNaCompra.Influenciador));
        var sergio = conta.AdicionarContato(new DadosDoContato("Sérgio Lima", Telefone: Telefone.De("(11) 3333-4444"), Papel: PapelNaCompra.Decisor));
        conta.DefinirPrincipal(sergio.Id);

        var registro = RegistroDeConta.De(conta, versao: 3, Agora);
        var lida = registro.ParaConta();

        Assert.Equal((conta.Id.Valor, 3, "Logística Rota Sul", "12ABC34501DE35"), (registro.Id, registro.Versao, registro.Nome, registro.CnpjOuCpf));
        Assert.Equal((conta.Id, conta.Nome, conta.Documento, conta.Principal?.Id), (lida.Id, lida.Nome, lida.Documento, lida.Principal?.Id));
        Assert.Equal(Contatos(conta), Contatos(lida));
    }

    [Fact]
    public void A_conta_sem_documento_e_sem_contato_tambem_volta()
    {
        var conta = new Conta("Rota Sul");

        var registro = RegistroDeConta.De(conta, versao: 1, Agora);
        var lida = registro.ParaConta();

        Assert.Null(registro.CnpjOuCpf);
        Assert.Equal((conta.Id, (Documento?)null, (Contato?)null), (lida.Id, lida.Documento, lida.Principal));
    }

    public static TheoryData<string> SituacoesDoLead => ["novo", "em qualificação", "qualificado", "descartado", "convertido"];

    [Theory]
    [MemberData(nameof(SituacoesDoLead))]
    public void O_lead_volta_na_situacao_em_que_estava(string situacao)
    {
        var lead = LeadNaSituacao(situacao);

        var registro = RegistroDeLead.De(lead, versao: 1, Agora);
        var lido = registro.ParaLead();

        Assert.Equal((lead.Nome, lead.Situacao), (registro.Nome, registro.Situacao));
        Assert.Equal(Descrever(lead), Descrever(lido));
    }

    [Fact]
    public void O_funil_volta_com_as_etapas_em_uso_as_arquivadas_e_as_finais()
    {
        var funil = Funil.PadraoDaConstrutora();
        funil.AdicionarEtapa(new DadosDaEtapa("Aprovação de crédito", 70, 20, Requisitos.QuemDecide | Requisitos.ValorEstimado));
        funil.ArquivarEtapa(funil.Abertas[1].Id);
        funil.RenomearEtapa(funil.Ganha.Id, "Contrato assinado");

        var registro = RegistroDeFunil.De(funil, versao: 1, Agora);
        var lido = registro.ParaFunil();

        Assert.Equal(funil.Nome, registro.Nome);
        Assert.Equal(Descrever(funil), Descrever(lido));
    }

    public static TheoryData<string> SituacoesDaVenda => ["aberta", "ganha", "perdida e reaberta", "perdida"];

    [Theory]
    [MemberData(nameof(SituacoesDaVenda))]
    public void A_oportunidade_volta_com_cada_passagem(string situacao)
    {
        var venda = VendaNaSituacao(situacao);

        var registro = RegistroDeOportunidade.De(venda, versao: 1, Agora);
        var lida = registro.ParaOportunidade();

        Assert.Equal(
            (venda.Conta.Valor, venda.Funil.Valor, venda.Titulo, venda.Responsavel, venda.Situacao),
            (registro.ContaId, registro.FunilId, registro.Titulo, registro.Responsavel, registro.Situacao));
        Assert.Equal(Descrever(venda), Descrever(lida));
        Assert.Equal(venda.Passagens, lida.Passagens);
    }

    public static TheoryData<string> SituacoesDaAtividade => ["pendente", "concluída", "cancelada"];

    [Theory]
    [MemberData(nameof(SituacoesDaAtividade))]
    public void A_atividade_volta_como_estava(string situacao)
    {
        var atividade = AtividadeNaSituacao(situacao);

        var registro = RegistroDeAtividade.De(atividade, versao: 1, Agora);
        var lida = registro.ParaAtividade();

        Assert.Equal(
            (atividade.Conta.Valor, atividade.Oportunidade?.Valor, atividade.Responsavel, atividade.Situacao, atividade.PrevistaPara),
            (registro.ContaId, registro.OportunidadeId, registro.Responsavel, registro.Situacao, registro.PrevistaPara));
        Assert.Equal(TimeSpan.Zero, registro.PrevistaPara.Offset);
        Assert.Equal(Descrever(atividade), Descrever(lida));
    }

    [Fact]
    public void A_proposta_volta_com_cada_versao()
    {
        var proposta = PropostasDeTeste.DoGalpao(PropostasDeTeste.Venda());
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Agora);
        proposta.Recusar("Caro", Agora.AddDays(1));
        proposta.NovaVersao();
        proposta.Redigir(PropostasDeTeste.DoGalpao(8m) with { CondicoesDePagamento = null });
        proposta.AprovarDesconto(PropostasDeTeste.Helena, Alcadas.Padrao, Agora.AddDays(2));
        proposta.Enviar(PropostasDeTeste.Carlos, Alcadas.Padrao, Agora.AddDays(2));
        proposta.Aceitar(Agora.AddDays(3));

        var registro = RegistroDeProposta.De(proposta, versao: 1, Agora);
        var lida = registro.ParaProposta();

        Assert.Equal((proposta.Oportunidade.Valor, proposta.Conta.Valor), (registro.OportunidadeId, registro.ContaId));
        Assert.Equal(Descrever(proposta), Descrever(lida));
    }

    private static List<string> Contatos(Conta conta) =>
        [.. conta.Contatos.Select(contato => $"{contato.Id} {contato.Nome} {contato.Email} {contato.Telefone} {contato.Cargo} {contato.Papel}")];

    private static string Descrever(Lead lead) =>
        $"{lead.Id} {lead.Nome} {lead.Empresa} {lead.Documento} {lead.Email} {lead.Telefone} {lead.Interesse} {lead.Origem} {lead.DetalheDaOrigem} {lead.ChegouEm:O} "
        + $"{lead.Situacao} {lead.Avaliacao} {lead.QualificadoEm:O} {lead.MotivoDoDescarte} {lead.ObservacaoDoDescarte} {lead.DescartadoEm:O} {lead.Conta} {lead.Contato} {lead.ConvertidoEm:O}";

    private static string[] Descrever(Funil funil) =>
        [
            $"{funil.Id} {funil.Nome}",
            .. funil.Abertas.Concat(funil.Arquivadas).Append(funil.Ganha).Append(funil.Perdida).Select(etapa =>
                $"{etapa.Id} {etapa.Tipo} {etapa.Nome} {etapa.Probabilidade} {etapa.DiasEsperados} {etapa.Requisitos} {etapa.Arquivada}"),
        ];

    private static string Descrever(Oportunidade venda) =>
        $"{venda.Id} {venda.Conta} {venda.Titulo} {venda.Funil} {venda.Responsavel} {venda.CriadaEm:O} {venda.Lead} {venda.Situacao} {venda.Etapa} "
        + $"{venda.ValorEstimado} {venda.PrevisaoDeFechamento} {venda.QuemDecide} {venda.ValorFechado} {venda.Perda} {venda.FechadaEm:O}";

    private static string Descrever(Atividade atividade) =>
        $"{atividade.Id} {atividade.Conta} {atividade.Oportunidade} {atividade.Tipo} {atividade.Assunto} {atividade.Com} {atividade.Detalhes} {atividade.CriadaEm:O} "
        + $"{atividade.PrevistaPara:O} {atividade.Adiamentos} {atividade.Responsavel} {atividade.Situacao} {atividade.Resultado} {atividade.MotivoDoCancelamento} {atividade.EncerradaEm:O}";

    private static string[] Descrever(Proposta proposta) =>
        [
            $"{proposta.Id} {proposta.Oportunidade} {proposta.Conta} {proposta.CriadaEm:O}",
            .. proposta.Versoes.Select(versao =>
                $"{versao.Numero} {versao.Situacao} {string.Join("; ", versao.Itens)} {versao.Desconto} {versao.ValidaAte} {versao.CondicoesDePagamento} "
                + $"{versao.Aprovacao} {versao.EnviadaPor} {versao.EnviadaEm:O} {versao.RespondidaEm:O} {versao.MotivoDaRecusa} {versao.Total}"),
        ];

    private static Lead LeadNaSituacao(string situacao)
    {
        var lead = situacao is "novo" or "em qualificação" ? LeadsDeTeste.DoSite() : LeadsDeTeste.Qualificado();

        switch (situacao)
        {
            case "em qualificação":
                lead.Avaliar(LeadsDeTeste.QueQualifica with { Orcamento = Resposta.NaoSabido });
                break;
            case "descartado":
                lead.Descartar(MotivoDoDescarte.Outro, "Só queria o preço", LeadsDeTeste.Segunda.AddDays(3));
                break;
            case "convertido":
                lead.ConverterEmContaNova([], LeadsDeTeste.Segunda.AddDays(4));
                break;
        }

        return lead;
    }

    private static Oportunidade VendaNaSituacao(string situacao)
    {
        var funil = Funil.PadraoDaConstrutora();
        var conta = VendasDeTeste.RotaSul();
        var venda = VendasDeTeste.Completa(funil, conta);
        venda.MoverPara(funil, funil.Abertas[3].Id, VendasDeTeste.Segunda.AddDays(5));

        switch (situacao)
        {
            case "ganha":
                venda.Ganhar(funil, Dinheiro.Reais(2_350_000.10m), VendasDeTeste.Segunda.AddDays(20));
                break;
            case "perdida":
                venda.Perder(funil, new Perda(MotivoDaPerda.ConcorrenteEscolhido, "Construtora Beta", "Prazo menor"), VendasDeTeste.Segunda.AddDays(20));
                break;
            case "perdida e reaberta":
                venda.Perder(funil, new Perda(MotivoDaPerda.Preco), VendasDeTeste.Segunda.AddDays(20));
                venda.Reabrir(funil, funil.Abertas[2].Id, VendasDeTeste.Segunda.AddDays(30));
                break;
        }

        return venda;
    }

    private static Atividade AtividadeNaSituacao(string situacao)
    {
        var funil = Funil.PadraoDaConstrutora();
        var conta = VendasDeTeste.RotaSul();
        var atividade = new Atividade(
            conta,
            new DadosDaAtividade(TipoDeAtividade.Reuniao, "Apresentar a proposta", VendasDeTeste.Segunda.AddDays(2), conta.Contatos[1].Id, "Levar a maquete"),
            "carlos.souza",
            VendasDeTeste.Segunda,
            situacao == "pendente" ? VendasDeTeste.DoGalpao(funil, conta) : null);

        switch (situacao)
        {
            case "pendente":
                atividade.Reagendar(VendasDeTeste.Segunda.AddDays(5), VendasDeTeste.Segunda.AddDays(1));
                break;
            case "concluída":
                atividade.Concluir("Gostaram; pediram desconto", VendasDeTeste.Segunda.AddDays(2));
                break;
            case "cancelada":
                atividade.Cancelar("Diretor viajou", VendasDeTeste.Segunda.AddDays(1));
                break;
        }

        return atividade;
    }
}
