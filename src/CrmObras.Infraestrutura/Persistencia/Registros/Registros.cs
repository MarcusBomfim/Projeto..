using CrmObras.Dominio.Atividades;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Dominio.Propostas;
using CrmObras.Infraestrutura.Documentos;

namespace CrmObras.Infraestrutura.Persistencia.Registros;

/*
 * Um registro por agregado. Cada um sabe virar do agregado — serializando o
 * documento e copiando para as colunas o que o banco precisa enxergar — e
 * voltar para ele, conferindo o formato e passando pela reconstituição do
 * domínio (ADR-0010). As referências entre agregados vivem nas colunas, onde
 * o banco as confere, e não no documento.
 */

internal sealed class RegistroDeConta : RegistroComNome
{
    /// <summary>O CNPJ ou o CPF, sem máscara: único na construtora, porque identifica o cliente (ADR-0002).</summary>
    public string? CnpjOuCpf { get; set; }

    /// <summary>O nome como a busca de duplicados o compara; nulo nas contas gravadas antes de a coluna existir, até a próxima gravação.</summary>
    public string? NomeComparavel { get; set; }

    public static RegistroDeConta De(Conta conta, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeConta
        {
            Nome = conta.Nome,
            CnpjOuCpf = conta.Documento?.Numero,
            NomeComparavel = BuscaDeDuplicados.NomeComparavel(conta.Nome),
        };
        var documento = Json.Escrever(DocumentoDaConta.De(conta), ContextoJson.Default.DocumentoDaConta);

        return registro.Preencher<RegistroDeConta>(conta.Id.Valor, versao, DocumentoDaConta.Formato, documento, agora);
    }

    public Conta ParaConta()
    {
        ConferirFormato(DocumentoDaConta.Formato, "da conta");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaConta).ParaConta(new ContaId(Id));
    }
}

internal sealed class RegistroDeLead : RegistroComNome
{
    public SituacaoDoLead Situacao { get; set; }

    public static RegistroDeLead De(Lead lead, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeLead { Nome = lead.Nome, Situacao = lead.Situacao };
        var documento = Json.Escrever(DocumentoDoLead.De(lead), ContextoJson.Default.DocumentoDoLead);

        return registro.Preencher<RegistroDeLead>(lead.Id.Valor, versao, DocumentoDoLead.Formato, documento, agora);
    }

    public Lead ParaLead()
    {
        ConferirFormato(DocumentoDoLead.Formato, "do lead");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDoLead).ParaLead(new LeadId(Id));
    }
}

internal sealed class RegistroDeFunil : RegistroComNome
{
    public static RegistroDeFunil De(Funil funil, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeFunil { Nome = funil.Nome };
        var documento = Json.Escrever(DocumentoDoFunil.De(funil), ContextoJson.Default.DocumentoDoFunil);

        return registro.Preencher<RegistroDeFunil>(funil.Id.Valor, versao, DocumentoDoFunil.Formato, documento, agora);
    }

    public Funil ParaFunil()
    {
        ConferirFormato(DocumentoDoFunil.Formato, "do funil");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDoFunil).ParaFunil(new FunilId(Id));
    }
}

internal sealed class RegistroDeOportunidade : RegistroDeAgregado
{
    /// <summary>A conta da venda — chave estrangeira.</summary>
    public Guid ContaId { get; set; }

    /// <summary>O funil por onde a venda anda — chave estrangeira.</summary>
    public Guid FunilId { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string Responsavel { get; set; } = string.Empty;

    public SituacaoDaOportunidade Situacao { get; set; }

    public static RegistroDeOportunidade De(Oportunidade oportunidade, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeOportunidade
        {
            ContaId = oportunidade.Conta.Valor,
            FunilId = oportunidade.Funil.Valor,
            Titulo = oportunidade.Titulo,
            Responsavel = oportunidade.Responsavel,
            Situacao = oportunidade.Situacao,
        };
        var documento = Json.Escrever(DocumentoDaOportunidade.De(oportunidade), ContextoJson.Default.DocumentoDaOportunidade);

        return registro.Preencher<RegistroDeOportunidade>(oportunidade.Id.Valor, versao, DocumentoDaOportunidade.Formato, documento, agora);
    }

    public Oportunidade ParaOportunidade()
    {
        ConferirFormato(DocumentoDaOportunidade.Formato, "da oportunidade");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaOportunidade)
            .ParaOportunidade(new OportunidadeId(Id), new ContaId(ContaId), new FunilId(FunilId));
    }
}

internal sealed class RegistroDeAtividade : RegistroDeAgregado
{
    /// <summary>A conta — chave estrangeira.</summary>
    public Guid ContaId { get; set; }

    /// <summary>A venda a que a atividade serve, se serve a uma — chave estrangeira.</summary>
    public Guid? OportunidadeId { get; set; }

    public string Responsavel { get; set; } = string.Empty;

    public SituacaoDaAtividade Situacao { get; set; }

    /// <summary>Quando, em UTC: é por aqui que a agenda procura, sem abrir documento. O fuso de quem marcou fica no documento.</summary>
    public DateTimeOffset PrevistaPara { get; set; }

    public static RegistroDeAtividade De(Atividade atividade, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeAtividade
        {
            ContaId = atividade.Conta.Valor,
            OportunidadeId = atividade.Oportunidade?.Valor,
            Responsavel = atividade.Responsavel,
            Situacao = atividade.Situacao,
            PrevistaPara = atividade.PrevistaPara.ToUniversalTime(),
        };
        var documento = Json.Escrever(DocumentoDaAtividade.De(atividade), ContextoJson.Default.DocumentoDaAtividade);

        return registro.Preencher<RegistroDeAtividade>(atividade.Id.Valor, versao, DocumentoDaAtividade.Formato, documento, agora);
    }

    public Atividade ParaAtividade()
    {
        ConferirFormato(DocumentoDaAtividade.Formato, "da atividade");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaAtividade)
            .ParaAtividade(new AtividadeId(Id), new ContaId(ContaId), OportunidadeId is { } venda ? new OportunidadeId(venda) : null);
    }
}

internal sealed class RegistroDeProposta : RegistroDeAgregado
{
    /// <summary>A venda — chave estrangeira.</summary>
    public Guid OportunidadeId { get; set; }

    /// <summary>A conta da venda — chave estrangeira, para listar as propostas de um cliente sem passar pelas vendas.</summary>
    public Guid ContaId { get; set; }

    public static RegistroDeProposta De(Proposta proposta, int versao, DateTimeOffset agora)
    {
        var registro = new RegistroDeProposta { OportunidadeId = proposta.Oportunidade.Valor, ContaId = proposta.Conta.Valor };
        var documento = Json.Escrever(DocumentoDaProposta.De(proposta), ContextoJson.Default.DocumentoDaProposta);

        return registro.Preencher<RegistroDeProposta>(proposta.Id.Valor, versao, DocumentoDaProposta.Formato, documento, agora);
    }

    public Proposta ParaProposta()
    {
        ConferirFormato(DocumentoDaProposta.Formato, "da proposta");

        return Json.Ler(Documento, ContextoJson.Default.DocumentoDaProposta)
            .ParaProposta(new PropostaId(Id), new OportunidadeId(OportunidadeId), new ContaId(ContaId));
    }
}
