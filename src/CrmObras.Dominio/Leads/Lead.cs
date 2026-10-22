using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.MeiosDeContato;

namespace CrmObras.Dominio.Leads;

/// <summary>O que o interessado deixou quando chegou.</summary>
/// <param name="Nome">Quem procurou a construtora.</param>
/// <param name="Empresa">A empresa dele, se é empresa; nulo se é para ele mesmo.</param>
/// <param name="Documento">CNPJ ou CPF, se ele informou.</param>
/// <param name="Email">E-mail; o lead precisa de e-mail ou de telefone.</param>
/// <param name="Telefone">Telefone; o lead precisa de e-mail ou de telefone.</param>
/// <param name="Interesse">A obra, como ele descreveu: "galpão de 1.200 m² em Jundiaí".</param>
public sealed record DadosDoLead(
    string Nome,
    string? Empresa = null,
    Documento? Documento = null,
    Email? Email = null,
    Telefone? Telefone = null,
    string? Interesse = null);

/// <summary>
/// Um interessado que ainda não é cliente: chegou por um canal, passa pelas
/// perguntas da qualificação e vira conta — ou é descartado, com o motivo
/// (ADR-0003).
/// </summary>
/// <remarks>
/// <para>
/// Qualifica quando a necessidade e o orçamento estão confirmados, e há quem
/// decida na conversa ou um prazo definido. Só o lead qualificado vira conta.
/// </para>
/// <para>
/// Converter em conta nova exige ter procurado os duplicados antes: a lista do
/// que se encontrou é parâmetro, e se nela houver uma conta com o mesmo
/// documento, a conversão é recusada — o lead vira contato dela.
/// </para>
/// </remarks>
public sealed class Lead
{
    private const int MaiorNome = 200;

    private const int MaiorTexto = 1000;

    public Lead(DadosDoLead dados, OrigemDoLead origem, string? detalheDaOrigem, DateTimeOffset chegouEm)
        : this(LeadId.Novo(), dados, origem, detalheDaOrigem, chegouEm)
    {
    }

    private Lead(LeadId id, DadosDoLead dados, OrigemDoLead origem, string? detalheDaOrigem, DateTimeOffset chegouEm)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(dados);

        var problemas = new List<string>();
        Acrescentar(problemas, Textos.Problema(dados.Nome, "o nome de quem procurou", MaiorNome));
        Acrescentar(problemas, Textos.Opcional(dados.Empresa)?.Length > MaiorNome ? $"O nome da empresa passa de {MaiorNome} caracteres." : null);
        Acrescentar(problemas, Textos.Opcional(dados.Interesse)?.Length > MaiorTexto ? $"O interesse passa de {MaiorTexto} caracteres." : null);
        Acrescentar(problemas, Textos.Opcional(detalheDaOrigem)?.Length > MaiorNome ? $"O detalhe da origem passa de {MaiorNome} caracteres." : null);
        Acrescentar(problemas, dados.Email is null && dados.Telefone is null
            ? "O lead precisa de um e-mail ou de um telefone: sem um dos dois, não há como responder."
            : null);
        Acrescentar(problemas, origem is OrigemDoLead.Indicacao or OrigemDoLead.Evento && Textos.Opcional(detalheDaOrigem) is null
            ? (origem == OrigemDoLead.Indicacao ? "Lead de indicação diz quem indicou." : "Lead de evento diz qual evento.")
            : null);

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Id = id;
        Nome = dados.Nome.Trim();
        Empresa = Textos.Opcional(dados.Empresa);
        Documento = dados.Documento;
        Email = dados.Email;
        Telefone = dados.Telefone;
        Interesse = Textos.Opcional(dados.Interesse);
        Origem = origem;
        DetalheDaOrigem = Textos.Opcional(detalheDaOrigem);
        ChegouEm = chegouEm;
    }

    public LeadId Id { get; }

    public string Nome { get; }

    public string? Empresa { get; }

    public Documento? Documento { get; }

    public Email? Email { get; }

    public Telefone? Telefone { get; }

    public string? Interesse { get; }

    public OrigemDoLead Origem { get; }

    /// <summary>Quem indicou, qual feira, qual campanha.</summary>
    public string? DetalheDaOrigem { get; }

    public DateTimeOffset ChegouEm { get; }

    public SituacaoDoLead Situacao { get; private set; }

    public Avaliacao Avaliacao { get; private set; } = Avaliacao.NaoFeita;

    public DateTimeOffset? QualificadoEm { get; private set; }

    public MotivoDoDescarte? MotivoDoDescarte { get; private set; }

    public string? ObservacaoDoDescarte { get; private set; }

    public DateTimeOffset? DescartadoEm { get; private set; }

    /// <summary>A conta em que o lead virou cliente.</summary>
    public ContaId? Conta { get; private set; }

    /// <summary>O contato que o lead virou, dentro da conta.</summary>
    public ContatoId? Contato { get; private set; }

    public DateTimeOffset? ConvertidoEm { get; private set; }

    /// <summary>Como o cliente se chama: a empresa, ou a pessoa, se é para ela mesma.</summary>
    public string NomeDoCliente => Empresa ?? Nome;

    /// <summary>O que mudou desde que o lead chegou: a situação e o que ela carrega.</summary>
    internal EstadoDoLead Estado =>
        new(Situacao, Avaliacao, QualificadoEm, MotivoDoDescarte, ObservacaoDoDescarte, DescartadoEm, Conta, Contato, ConvertidoEm);

    /// <summary>Registra as respostas da qualificação. Avaliar de novo um lead qualificado o devolve à qualificação.</summary>
    public void Avaliar(Avaliacao avaliacao)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        Exigir("avaliado", SituacaoDoLead.Novo, SituacaoDoLead.EmQualificacao, SituacaoDoLead.Qualificado);

        Avaliacao = avaliacao;
        Situacao = SituacaoDoLead.EmQualificacao;
        QualificadoEm = null;
    }

    /// <summary>Dá o lead por qualificado, se a avaliação permite; se não, diz o que falta.</summary>
    public void Qualificar(DateTimeOffset em)
    {
        Exigir("qualificado", SituacaoDoLead.Novo, SituacaoDoLead.EmQualificacao, SituacaoDoLead.Qualificado);

        if (Situacao == SituacaoDoLead.Qualificado)
        {
            return;
        }

        if (!Avaliacao.Qualifica)
        {
            throw new ExcecaoDeDominio($"O lead {Nome} ainda não se qualifica: falta {Textos.Lista(Avaliacao.Pendencias)}.");
        }

        Situacao = SituacaoDoLead.Qualificado;
        QualificadoEm = em;
    }

    public void Descartar(MotivoDoDescarte motivo, string? observacao, DateTimeOffset em)
    {
        Exigir("descartado", SituacaoDoLead.Novo, SituacaoDoLead.EmQualificacao, SituacaoDoLead.Qualificado);

        if (motivo == Leads.MotivoDoDescarte.Outro && Textos.Opcional(observacao) is null)
        {
            throw new ExcecaoDeDominio("Descarte por outro motivo diz qual, na observação.");
        }

        if (Textos.Opcional(observacao)?.Length > MaiorTexto)
        {
            throw new ExcecaoDeDominio($"A observação passa de {MaiorTexto} caracteres.");
        }

        Situacao = SituacaoDoLead.Descartado;
        MotivoDoDescarte = motivo;
        ObservacaoDoDescarte = Textos.Opcional(observacao);
        DescartadoEm = em;
        QualificadoEm = null;
    }

    /// <summary>O lead descartado volta à qualificação, com a avaliação que tinha.</summary>
    public void Reabrir()
    {
        if (Situacao != SituacaoDoLead.Descartado)
        {
            throw new ExcecaoDeDominio($"O lead {Nome} não foi descartado: não há o que reabrir.");
        }

        Situacao = SituacaoDoLead.EmQualificacao;
        MotivoDoDescarte = null;
        ObservacaoDoDescarte = null;
        DescartadoEm = null;
    }

    /// <summary>
    /// O lead qualificado vira uma conta nova, com ele de contato principal.
    /// </summary>
    /// <param name="duplicados">
    /// O que a busca de duplicados encontrou para este lead. Havendo conta com
    /// o mesmo documento, a conversão é recusada: o lead vira contato dela.
    /// </param>
    public Conta ConverterEmContaNova(IReadOnlyList<PossivelDuplicado> duplicados, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(duplicados);
        Exigir("convertido", SituacaoDoLead.Qualificado);

        if (duplicados.FirstOrDefault(duplicado => duplicado.MaisForte == Semelhanca.MesmoDocumento) is { } mesmoCliente)
        {
            throw new ExcecaoDeDominio(
                $"Já existe a conta {mesmoCliente.NomeDaConta} com o documento {Documento}: converta o lead nela.");
        }

        var conta = new Conta(NomeDoCliente, Documento);
        Converter(conta.Id, conta.AdicionarContato(ComoContato()).Id, em);

        return conta;
    }

    /// <summary>
    /// O lead qualificado vira contato de uma conta que já existe — ou,
    /// se o e-mail dele já é de um contato da conta, é esse contato.
    /// </summary>
    public Contato ConverterNaConta(Conta conta, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(conta);
        Exigir("convertido", SituacaoDoLead.Qualificado);

        if (Documento is not null && conta.Documento is not null && Documento != conta.Documento)
        {
            throw new ExcecaoDeDominio(
                $"O lead é do cliente com o documento {Documento}, e a conta {conta.Nome} tem o documento {conta.Documento}: não são o mesmo cliente.");
        }

        if (Documento is not null)
        {
            conta.InformarDocumento(Documento);
        }

        var contato = conta.Contatos.FirstOrDefault(existente => Email is not null && existente.Email == Email)
            ?? conta.AdicionarContato(ComoContato());
        Converter(conta.Id, contato.Id, em);

        return contato;
    }

    public override string ToString() => Nome;

    /// <summary>
    /// Refaz um lead gravado, com a identidade dele (ADR-0010). Os passos são
    /// dados de novo, pelas regras de sempre — avaliar, qualificar, descartar —,
    /// e o estado a que eles chegam tem de ser o gravado: o que as regras não
    /// produzem é recusado.
    /// </summary>
    internal static Lead Reconstituir(
        LeadId id,
        DadosDoLead dados,
        OrigemDoLead origem,
        string? detalheDaOrigem,
        DateTimeOffset chegouEm,
        EstadoDoLead estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        var lead = new Lead(id, dados, origem, detalheDaOrigem, chegouEm);

        if (estado.Avaliacao != Avaliacao.NaoFeita || estado.Situacao is SituacaoDoLead.EmQualificacao)
        {
            lead.Avaliar(estado.Avaliacao);
        }

        switch (estado.Situacao)
        {
            case SituacaoDoLead.Qualificado:
                lead.Qualificar(lead.Exigido(estado.QualificadoEm, estado));
                break;
            case SituacaoDoLead.Descartado:
                lead.Descartar(lead.Exigido(estado.MotivoDoDescarte, estado), estado.ObservacaoDoDescarte, lead.Exigido(estado.DescartadoEm, estado));
                break;
            case SituacaoDoLead.Convertido:
                lead.Qualificar(lead.Exigido(estado.QualificadoEm, estado));
                lead.Converter(lead.Exigido(estado.Conta, estado), lead.Exigido(estado.Contato, estado), lead.Exigido(estado.ConvertidoEm, estado));
                break;
        }

        return lead.Estado == estado ? lead : throw lead.Incoerente(estado);
    }

    private static void Acrescentar(List<string> problemas, string? problema)
    {
        if (problema is not null)
        {
            problemas.Add(problema);
        }
    }

    private DadosDoContato ComoContato() => new(Nome, Email, Telefone);

    private void Converter(ContaId conta, ContatoId contato, DateTimeOffset em)
    {
        Situacao = SituacaoDoLead.Convertido;
        Conta = conta;
        Contato = contato;
        ConvertidoEm = em;
    }

    private T Exigido<T>(T? valor, EstadoDoLead estado)
        where T : class => valor ?? throw Incoerente(estado);

    private T Exigido<T>(T? valor, EstadoDoLead estado)
        where T : struct => valor ?? throw Incoerente(estado);

    private ExcecaoDeDominio Incoerente(EstadoDoLead estado) =>
        new($"O lead {Nome} está gravado como {estado.Situacao}, num estado que as regras não produzem.");

    private void Exigir(string oQue, params SituacaoDoLead[] situacoes)
    {
        if (!situacoes.Contains(Situacao))
        {
            throw new ExcecaoDeDominio(Situacao switch
            {
                SituacaoDoLead.Convertido => $"O lead {Nome} já virou cliente, e não pode ser {oQue}: a história segue na conta.",
                SituacaoDoLead.Descartado => $"O lead {Nome} foi descartado, e não pode ser {oQue}: reabra antes.",
                _ => $"O lead {Nome} ainda não foi qualificado, e não pode ser {oQue}.",
            });
        }
    }
}

/// <summary>O que muda num lead depois que ele chega: a situação e o que ela carrega.</summary>
internal sealed record EstadoDoLead(
    SituacaoDoLead Situacao,
    Avaliacao Avaliacao,
    DateTimeOffset? QualificadoEm = null,
    MotivoDoDescarte? MotivoDoDescarte = null,
    string? ObservacaoDoDescarte = null,
    DateTimeOffset? DescartadoEm = null,
    ContaId? Conta = null,
    ContatoId? Contato = null,
    DateTimeOffset? ConvertidoEm = null);
