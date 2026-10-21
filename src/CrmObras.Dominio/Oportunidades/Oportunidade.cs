using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;

namespace CrmObras.Dominio.Oportunidades;

/// <summary>
/// Uma venda em andamento: uma obra que um cliente pode fechar, andando pelas
/// etapas de um funil até ser ganha ou perdida (ADR-0005).
/// </summary>
/// <remarks>
/// <para>
/// Nasce na primeira etapa do funil, e só entra numa etapa se tiver o que
/// ela pede — e o que pedem as anteriores (ADR-0004). Cada passagem por uma
/// etapa fica guardada, com a entrada e a saída: é de onde sai o tempo em
/// cada etapa, e a venda parada.
/// </para>
/// <para>
/// Ganha, a venda está fechada e não muda mais: virou obra. Perdida, guarda o
/// motivo, e pode ser reaberta numa etapa aberta — o cliente que voltou atrás.
/// </para>
/// </remarks>
public sealed class Oportunidade
{
    private const int MaiorTitulo = 200;

    private const int MaiorResponsavel = 100;

    private const int MaiorConcorrente = 200;

    private const int MaiorTexto = 1000;

    private readonly List<PassagemPorEtapa> _passagens = [];

    /// <param name="conta">O cliente.</param>
    /// <param name="titulo">A obra, em poucas palavras: "Galpão de 1.200 m² em Jundiaí".</param>
    /// <param name="funil">Por onde a venda vai andar. Ela nasce na primeira etapa.</param>
    /// <param name="responsavel">Quem cuida da venda: o login do vendedor.</param>
    /// <param name="em">Quando a venda começou.</param>
    /// <param name="lead">O lead de onde a venda veio, se veio de um.</param>
    public Oportunidade(ContaId conta, string titulo, Funil funil, string responsavel, DateTimeOffset em, LeadId? lead = null)
    {
        ArgumentNullException.ThrowIfNull(conta);
        ArgumentNullException.ThrowIfNull(funil);

        var problemas = new[]
        {
            Textos.Problema(titulo, "o título da oportunidade", MaiorTitulo),
            Textos.Problema(responsavel, "o responsável pela oportunidade", MaiorResponsavel),
        }.OfType<string>().ToList();

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Id = OportunidadeId.Novo();
        Conta = conta;
        Titulo = titulo.Trim();
        Funil = funil.Id;
        Responsavel = responsavel.Trim();
        CriadaEm = em;
        Lead = lead;
        _passagens.Add(new PassagemPorEtapa(funil.Primeira.Id, em));
    }

    public OportunidadeId Id { get; }

    public ContaId Conta { get; }

    public string Titulo { get; private set; }

    public FunilId Funil { get; }

    public string Responsavel { get; private set; }

    public DateTimeOffset CriadaEm { get; }

    /// <summary>O lead de onde a venda veio: é o que diz que canal traz venda, e não só contato.</summary>
    public LeadId? Lead { get; }

    public SituacaoDaOportunidade Situacao { get; private set; }

    /// <summary>Onde a venda está: uma etapa aberta, a Ganha ou a Perdida.</summary>
    public EtapaId Etapa => _passagens[^1].Etapa;

    public DateTimeOffset EntrouNaEtapaEm => _passagens[^1].EntrouEm;

    /// <summary>Cada etapa por onde a venda passou, na ordem. A última é a atual, sem saída.</summary>
    public IReadOnlyList<PassagemPorEtapa> Passagens => _passagens;

    /// <summary>Quanto a obra deve valer, enquanto não fecha.</summary>
    public Dinheiro? ValorEstimado { get; private set; }

    public DateOnly? PrevisaoDeFechamento { get; private set; }

    /// <summary>O contato do cliente que decide a compra.</summary>
    public ContatoId? QuemDecide { get; private set; }

    /// <summary>Por quanto a obra foi vendida.</summary>
    public Dinheiro? ValorFechado { get; private set; }

    public Perda? Perda { get; private set; }

    /// <summary>Quando foi ganha ou perdida.</summary>
    public DateTimeOffset? FechadaEm { get; private set; }

    public void Renomear(string titulo)
    {
        if (Textos.Problema(titulo, "o título da oportunidade", MaiorTitulo) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Titulo = titulo.Trim();
    }

    /// <summary>Passa a venda a outro vendedor — fechada inclusive: quem sai da empresa passa a carteira inteira.</summary>
    public void TransferirPara(string responsavel)
    {
        if (Textos.Problema(responsavel, "o responsável pela oportunidade", MaiorResponsavel) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Responsavel = responsavel.Trim();
    }

    public void Estimar(Dinheiro valor)
    {
        ExigirAberta();

        if (ProblemaDoValor(valor, "valor estimado") is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        ValorEstimado = valor;
    }

    public void PreverFechamento(DateOnly data)
    {
        ExigirAberta();
        PrevisaoDeFechamento = data;
    }

    /// <summary>Indica, entre os contatos da conta, quem decide a compra.</summary>
    public void DefinirQuemDecide(Conta conta, ContatoId contato)
    {
        ArgumentNullException.ThrowIfNull(conta);
        ArgumentNullException.ThrowIfNull(contato);
        ExigirAberta();

        if (conta.Id != Conta)
        {
            throw new ExcecaoDeDominio($"A oportunidade {Titulo} não é da conta {conta.Nome}: quem decide é um contato do cliente dela.");
        }

        QuemDecide = conta.Contato(contato).Id;
    }

    /// <summary>
    /// Leva a venda a outra etapa aberta, para a frente ou para trás, se ela
    /// tiver o que a etapa pede. Mover para onde já está não muda nada.
    /// </summary>
    public void MoverPara(Funil funil, EtapaId destino, DateTimeOffset em)
    {
        ExigirAberta();

        var etapa = EtapaAberta(funil, destino);

        if (etapa.Id == Etapa)
        {
            return;
        }

        ConferirEntrada(funil, etapa, em);
        Entrar(etapa.Id, em);
    }

    /// <summary>Fecha a venda, pelo valor vendido. Ganhar não pede requisito de etapa: pede o valor.</summary>
    public void Ganhar(Funil funil, Dinheiro valorFechado, DateTimeOffset em)
    {
        ExigirAberta();
        ConferirFunil(funil);
        ConferirInstante(em);

        if (ProblemaDoValor(valorFechado, "valor fechado") is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Situacao = SituacaoDaOportunidade.Ganha;
        ValorFechado = valorFechado;
        FechadaEm = em;
        Entrar(funil.Ganha.Id, em);
    }

    /// <summary>Dá a venda por perdida, com o motivo.</summary>
    public void Perder(Funil funil, Perda perda, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(perda);
        ExigirAberta();
        ConferirFunil(funil);
        ConferirInstante(em);

        var concorrente = Textos.Opcional(perda.Concorrente);
        var observacao = Textos.Opcional(perda.Observacao);
        var problemas = new List<string>();

        if (perda.Motivo == MotivoDaPerda.ConcorrenteEscolhido && concorrente is null)
        {
            problemas.Add("Perda para o concorrente diz qual concorrente.");
        }

        if (perda.Motivo == MotivoDaPerda.Outro && observacao is null)
        {
            problemas.Add("Perda por outro motivo diz qual, na observação.");
        }

        if (concorrente?.Length > MaiorConcorrente)
        {
            problemas.Add($"O nome do concorrente passa de {MaiorConcorrente} caracteres.");
        }

        if (observacao?.Length > MaiorTexto)
        {
            problemas.Add($"A observação passa de {MaiorTexto} caracteres.");
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Situacao = SituacaoDaOportunidade.Perdida;
        Perda = new Perda(perda.Motivo, concorrente, observacao);
        FechadaEm = em;
        Entrar(funil.Perdida.Id, em);
    }

    /// <summary>Devolve a venda perdida a uma etapa aberta, se ela tiver o que a etapa pede.</summary>
    public void Reabrir(Funil funil, EtapaId etapa, DateTimeOffset em)
    {
        if (Situacao == SituacaoDaOportunidade.Ganha)
        {
            throw new ExcecaoDeDominio($"A oportunidade {Titulo} foi ganha, e venda ganha não se reabre: virou obra.");
        }

        if (Situacao == SituacaoDaOportunidade.Aberta)
        {
            throw new ExcecaoDeDominio($"A oportunidade {Titulo} não foi perdida: não há o que reabrir.");
        }

        var destino = EtapaAberta(funil, etapa);
        ConferirEntrada(funil, destino, em);

        Situacao = SituacaoDaOportunidade.Aberta;
        Perda = null;
        FechadaEm = null;
        Entrar(destino.Id, em);
    }

    /// <summary>
    /// Quanto tempo a venda passou em cada etapa, na ordem em que entrou nelas
    /// pela primeira vez. Voltar a uma etapa soma ao tempo dela. Fechada a
    /// venda, o relógio para: o tempo na Ganha ou na Perdida não conta.
    /// </summary>
    public IReadOnlyList<TempoNaEtapa> TempoEmCadaEtapa(DateTimeOffset agora) =>
    [
        .. _passagens
            .Where(passagem => passagem.SaiuEm is not null || Situacao == SituacaoDaOportunidade.Aberta)
            .GroupBy(passagem => passagem.Etapa)
            .Select(passagens => new TempoNaEtapa(
                passagens.Key,
                passagens.Aggregate(TimeSpan.Zero, (tempo, passagem) => tempo + passagem.Duracao(agora)))),
    ];

    /// <summary>Há quanto tempo a venda está na etapa atual. Na fechada, há quanto tempo fechou.</summary>
    public TimeSpan TempoNaEtapaAtual(DateTimeOffset agora) => agora - EntrouNaEtapaEm;

    /// <summary>
    /// Se a venda está na etapa atual há mais dias do que a etapa espera. A
    /// fechada nunca está: a Ganha e a Perdida não têm prazo.
    /// </summary>
    public bool EstaParada(Funil funil, DateTimeOffset agora)
    {
        ConferirFunil(funil);

        return funil.Etapa(Etapa).DiasEsperados is { } dias && TempoNaEtapaAtual(agora) > TimeSpan.FromDays(dias);
    }

    public override string ToString() => Titulo;

    private static string? ProblemaDoValor(Dinheiro valor, string oQue) =>
        valor <= Dinheiro.Zero ? $"O {oQue} precisa ser maior que zero."
        : !valor.EstaEmCentavos ? $"O {oQue} vai até o centavo: {valor} tem casas demais."
        : null;

    private void ExigirAberta()
    {
        if (Situacao != SituacaoDaOportunidade.Aberta)
        {
            throw new ExcecaoDeDominio(Situacao == SituacaoDaOportunidade.Ganha
                ? $"A oportunidade {Titulo} foi ganha: a venda está fechada, e não muda mais."
                : $"A oportunidade {Titulo} foi perdida: reabra antes de mudá-la.");
        }
    }

    private void ConferirFunil(Funil funil)
    {
        ArgumentNullException.ThrowIfNull(funil);

        if (funil.Id != Funil)
        {
            throw new ExcecaoDeDominio($"A oportunidade {Titulo} não anda pelo funil {funil.Nome}.");
        }
    }

    /// <summary>A etapa aberta do funil da oportunidade. A Ganha e a Perdida têm caminho próprio: o valor fechado ou o motivo.</summary>
    private Etapa EtapaAberta(Funil funil, EtapaId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        ConferirFunil(funil);

        var etapa = funil.Etapa(id);

        if (etapa.EhFinal)
        {
            throw new ExcecaoDeDominio(etapa.Tipo == TipoDeEtapa.Ganha
                ? $"{etapa.Nome} é etapa final: a venda chega nela ganha, com o valor fechado."
                : $"{etapa.Nome} é etapa final: a venda chega nela perdida, com o motivo.");
        }

        return etapa;
    }

    /// <summary>Confere o instante e os requisitos acumulados da etapa — e diz tudo o que falta de uma vez.</summary>
    private void ConferirEntrada(Funil funil, Etapa etapa, DateTimeOffset em)
    {
        ConferirInstante(em);

        var requisitos = funil.RequisitosPara(etapa.Id);
        var faltam = new List<string>();

        if (requisitos.HasFlag(Requisitos.ValorEstimado) && ValorEstimado is null)
        {
            faltam.Add("o valor estimado");
        }

        if (requisitos.HasFlag(Requisitos.PrevisaoDeFechamento) && PrevisaoDeFechamento is null)
        {
            faltam.Add("a previsão de fechamento");
        }

        if (requisitos.HasFlag(Requisitos.QuemDecide) && QuemDecide is null)
        {
            faltam.Add("quem decide");
        }

        if (faltam.Count > 0)
        {
            throw new ExcecaoDeDominio($"Para a oportunidade {Titulo} entrar em {etapa.Nome}, falta {Textos.Lista(faltam)}.");
        }
    }

    private void ConferirInstante(DateTimeOffset em)
    {
        if (em < EntrouNaEtapaEm)
        {
            throw new ExcecaoDeDominio($"A oportunidade {Titulo} não pode sair da etapa antes de ter entrado nela.");
        }
    }

    private void Entrar(EtapaId etapa, DateTimeOffset em)
    {
        _passagens[^1] = _passagens[^1] with { SaiuEm = em };
        _passagens.Add(new PassagemPorEtapa(etapa, em));
    }
}
