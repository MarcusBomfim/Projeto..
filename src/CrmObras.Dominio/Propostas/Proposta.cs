using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Propostas;

/// <summary>
/// O que a construtora oferece ao cliente por uma venda: os itens, o preço, o
/// desconto e a validade — em versões, até o cliente aceitar (ADR-0007).
/// </summary>
/// <remarks>
/// <para>
/// A versão nasce em rascunho, e muda à vontade até ser enviada; enviada, não
/// muda mais. Para mudar o que o cliente já viu, cria-se uma versão nova, a
/// partir da última; enviada, ela substitui a anterior. O cliente aceita ou
/// recusa a versão que está com ele, dentro da validade.
/// </para>
/// <para>
/// O desconto acima da alçada de quem envia precisa da aprovação de quem tem
/// alçada para ele. A aprovação vale para o que foi aprovado: redigir a
/// versão de novo a desfaz. E a alçada é conferida no envio — se a
/// construtora a reduziu depois da aprovação, a aprovação não basta mais.
/// </para>
/// </remarks>
public sealed class Proposta
{
    private const int MaiorDescricao = 500;

    private const int MaiorUnidade = 10;

    private const int MaiorTexto = 1000;

    private readonly List<VersaoDaProposta> _versoes = [];

    /// <param name="oportunidade">A venda, que tem de estar aberta.</param>
    /// <param name="dados">O conteúdo da primeira versão, em rascunho.</param>
    /// <param name="em">Quando a proposta começou a ser redigida.</param>
    public Proposta(Oportunidade oportunidade, DadosDaVersao dados, DateTimeOffset em)
        : this(PropostaId.Novo(), Aberta(oportunidade).Id, oportunidade.Conta, em)
    {
        Conferir(dados);
        _versoes.Add(new VersaoDaProposta(1, dados));
    }

    private Proposta(PropostaId id, OportunidadeId oportunidade, ContaId conta, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(oportunidade);
        ArgumentNullException.ThrowIfNull(conta);

        Id = id;
        Oportunidade = oportunidade;
        Conta = conta;
        CriadaEm = em;
    }

    public PropostaId Id { get; }

    public OportunidadeId Oportunidade { get; }

    public ContaId Conta { get; }

    public DateTimeOffset CriadaEm { get; }

    /// <summary>Todas as versões, da primeira à última.</summary>
    public IReadOnlyList<VersaoDaProposta> Versoes => _versoes;

    /// <summary>A última versão: o rascunho, se há um; se não, a última enviada.</summary>
    public VersaoDaProposta Atual => _versoes[^1];

    /// <summary>A versão que está com o cliente, esperando resposta; nula se nenhuma está.</summary>
    public VersaoDaProposta? Vigente => _versoes.LastOrDefault(versao => versao.Situacao == SituacaoDaVersao.Enviada);

    /// <summary>A versão que o cliente aceitou; nula enquanto ele não aceita.</summary>
    public VersaoDaProposta? Aceita => _versoes.Find(versao => versao.Situacao == SituacaoDaVersao.Aceita);

    /// <summary>Troca o conteúdo do rascunho. Desfaz a aprovação do desconto, se havia.</summary>
    public void Redigir(DadosDaVersao dados)
    {
        ExigirRascunho();
        Conferir(dados);
        Atual.Redigir(dados);
    }

    /// <summary>Começa uma versão nova, em rascunho, igual à última enviada — para mudar o que o cliente já viu.</summary>
    public VersaoDaProposta NovaVersao()
    {
        ExigirAberta();

        if (Atual.Situacao == SituacaoDaVersao.Rascunho)
        {
            throw new ExcecaoDeDominio($"A versão {Atual.Numero} ainda é rascunho: redija nela.");
        }

        var nova = new VersaoDaProposta(Atual.Numero + 1, Atual.Conteudo());
        _versoes.Add(nova);

        return nova;
    }

    /// <summary>Aprova o desconto do rascunho, se cabe na alçada de quem aprova.</summary>
    public void AprovarDesconto(Comercial aprovador, Alcadas alcadas, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(aprovador);
        ArgumentNullException.ThrowIfNull(alcadas);
        ExigirRascunho();

        var desconto = Atual.Desconto;

        if (desconto == 0)
        {
            throw new ExcecaoDeDominio($"A versão {Atual.Numero} não tem desconto: não há o que aprovar.");
        }

        if (desconto > alcadas.De(aprovador.Cargo))
        {
            throw new ExcecaoDeDominio(alcadas.QuemDa(desconto) is { } cargo
                ? $"O {Alcadas.Nome(aprovador.Cargo)} aprova até {FormatoBrasileiro.Porcentagem(alcadas.De(aprovador.Cargo))}: o desconto de {FormatoBrasileiro.Porcentagem(desconto)} é do {Alcadas.Nome(cargo)}."
                : SemQuemAprove(desconto, alcadas));
        }

        Atual.Aprovar(new AprovacaoDeDesconto(aprovador, em));
    }

    /// <summary>
    /// Manda o rascunho ao cliente. A versão que estava com ele, se havia,
    /// é substituída.
    /// </summary>
    public void Enviar(Comercial quem, Alcadas alcadas, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(quem);
        ArgumentNullException.ThrowIfNull(alcadas);
        ExigirRascunho();

        var versao = Atual;
        var problemas = new List<string>();

        if (versao.Itens.Count == 0)
        {
            problemas.Add($"A versão {versao.Numero} não tem item: proposta sem item não se envia.");
        }

        if (versao.ValidaAte < Dia(em))
        {
            problemas.Add($"A versão {versao.Numero} vale até {FormatoBrasileiro.Data(versao.ValidaAte)}, antes do envio: a validade é de hoje em diante.");
        }

        if (ProblemaDoDesconto(versao, quem, alcadas) is { } desconto)
        {
            problemas.Add(desconto);
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Vigente?.Substituir();
        versao.Enviar(quem, em);
    }

    /// <summary>
    /// A versão que o cliente pode aceitar agora: a que está com ele, dentro
    /// da validade. Não muda nada — diz o que o aceite aceitaria, ou por que
    /// não pode.
    /// </summary>
    public VersaoDaProposta ConferirAceite(DateTimeOffset em)
    {
        ExigirAberta();

        var vigente = Vigente ?? throw new ExcecaoDeDominio("Nenhuma versão está com o cliente: não há o que aceitar.");

        if (Dia(em) > vigente.ValidaAte)
        {
            throw new ExcecaoDeDominio(
                $"A versão {vigente.Numero} venceu em {FormatoBrasileiro.Data(vigente.ValidaAte)}: para o cliente aceitar, envie uma versão nova.");
        }

        return vigente;
    }

    /// <summary>O cliente aceita a versão que está com ele. O rascunho que houver é descartado: a proposta está fechada.</summary>
    public void Aceitar(DateTimeOffset em)
    {
        var vigente = ConferirAceite(em);

        if (Atual.Situacao == SituacaoDaVersao.Rascunho)
        {
            _versoes.Remove(Atual);
        }

        vigente.Aceitar(em);
    }

    /// <summary>O cliente recusa a versão que está com ele, e diz por quê. Pode vir outra.</summary>
    public void Recusar(string motivo, DateTimeOffset em)
    {
        ExigirAberta();

        var vigente = Vigente ?? throw new ExcecaoDeDominio("Nenhuma versão está com o cliente: não há o que recusar.");

        if (Textos.Opcional(motivo) is null)
        {
            throw new ExcecaoDeDominio($"A recusa da versão {vigente.Numero} diz o motivo: é o que a versão nova vai responder.");
        }

        if (motivo.Trim().Length > MaiorTexto)
        {
            throw new ExcecaoDeDominio($"O motivo da recusa passa de {MaiorTexto} caracteres.");
        }

        vigente.Recusar(motivo.Trim(), em);
    }

    /// <summary>
    /// Refaz uma proposta gravada, com a identidade dela (ADR-0010). O
    /// conteúdo de cada versão passa pelas regras de quem redige; e as
    /// versões, juntas, têm de contar uma história que as regras produzem:
    /// numeradas em sequência, rascunho só na última, uma só com o cliente, a
    /// aceita no fim — e cada uma com o envio e a resposta que a situação dela
    /// pede.
    /// </summary>
    internal static Proposta Reconstituir(PropostaId id, OportunidadeId oportunidade, ContaId conta, DateTimeOffset criadaEm, IReadOnlyList<EstadoDaVersao> versoes)
    {
        ArgumentNullException.ThrowIfNull(versoes);

        var proposta = new Proposta(id, oportunidade, conta, criadaEm);

        foreach (var versao in versoes)
        {
            ArgumentNullException.ThrowIfNull(versao);
            Conferir(versao.Conteudo);
        }

        if (ProblemaDas(versoes) is { } problema)
        {
            throw new ExcecaoDeDominio($"A proposta {id} está gravada com versões que as regras não produzem: {problema}.");
        }

        proposta._versoes.AddRange(versoes.Select(VersaoDaProposta.Reconstituir));

        return proposta;
    }

    /// <summary>Se a versão que está com o cliente passou da validade.</summary>
    public bool EstaVencida(DateTimeOffset agora) => Vigente is { } vigente && Dia(agora) > vigente.ValidaAte;

    private static Oportunidade Aberta(Oportunidade oportunidade)
    {
        ArgumentNullException.ThrowIfNull(oportunidade);

        return oportunidade.Situacao == SituacaoDaOportunidade.Aberta
            ? oportunidade
            : throw new ExcecaoDeDominio($"A oportunidade {oportunidade.Titulo} está fechada: proposta é para venda em andamento.");
    }

    /// <summary>O que, nas versões gravadas, as regras não produziriam; nulo se nada.</summary>
    private static string? ProblemaDas(IReadOnlyList<EstadoDaVersao> versoes)
    {
        if (versoes.Count == 0)
        {
            return "nenhuma versão";
        }

        if (versoes.Select((versao, posicao) => versao.Numero == posicao + 1).Contains(false))
        {
            return "a numeração não é 1, 2, 3";
        }

        var enviadas = versoes.Where(versao => versao.Situacao != SituacaoDaVersao.Rascunho).ToList();

        if (enviadas.Count < versoes.Count - 1 || (enviadas.Count == versoes.Count - 1 && versoes[^1].Situacao != SituacaoDaVersao.Rascunho))
        {
            return "rascunho fora da última versão";
        }

        if (enviadas.Take(enviadas.Count - 1).Any(versao => versao.Situacao is not (SituacaoDaVersao.Substituida or SituacaoDaVersao.Recusada))
            || enviadas is [.., { Situacao: SituacaoDaVersao.Substituida }])
        {
            return "versão com o cliente, ou aceita, antes da última enviada";
        }

        if (enviadas is [.., { Situacao: SituacaoDaVersao.Aceita }] && enviadas.Count != versoes.Count)
        {
            return "rascunho depois da aceita";
        }

        return versoes.FirstOrDefault(versao => !Coerente(versao)) is { } incoerente
            ? $"a versão {incoerente.Numero} não tem o envio ou a resposta que a situação dela pede"
            : null;
    }

    /// <summary>Se o envio, a resposta e a aprovação são os que a situação da versão pede.</summary>
    private static bool Coerente(EstadoDaVersao versao)
    {
        var enviada = versao.EnviadaPor is not null && versao.EnviadaEm is not null;
        var respondida = versao.RespondidaEm >= versao.EnviadaEm;
        var aprovacao = versao.Aprovacao is null || versao.Conteudo.Desconto > 0;

        return aprovacao && versao.Situacao switch
        {
            SituacaoDaVersao.Rascunho => versao is { EnviadaPor: null, EnviadaEm: null, RespondidaEm: null, MotivoDaRecusa: null },
            SituacaoDaVersao.Enviada or SituacaoDaVersao.Substituida => enviada && versao is { RespondidaEm: null, MotivoDaRecusa: null },
            SituacaoDaVersao.Aceita => enviada && respondida && versao.MotivoDaRecusa is null,
            _ => enviada && respondida && Textos.Opcional(versao.MotivoDaRecusa) == versao.MotivoDaRecusa && versao.MotivoDaRecusa?.Length <= MaiorTexto,
        };
    }

    private static DateOnly Dia(DateTimeOffset instante) => DateOnly.FromDateTime(instante.DateTime);

    private static string SemQuemAprove(decimal desconto, Alcadas alcadas) =>
        $"O desconto de {FormatoBrasileiro.Porcentagem(desconto)} passa da maior alçada, de {FormatoBrasileiro.Porcentagem(alcadas.Diretor)} do diretor: não há quem aprove.";

    /// <summary>O desconto que passa da alçada de quem envia, sem aprovação de quem tem alçada para ele.</summary>
    private static string? ProblemaDoDesconto(VersaoDaProposta versao, Comercial quem, Alcadas alcadas)
    {
        var desconto = versao.Desconto;

        if (desconto <= alcadas.De(quem.Cargo)
            || (versao.Aprovacao is { } aprovacao && desconto <= alcadas.De(aprovacao.Aprovador.Cargo)))
        {
            return null;
        }

        return alcadas.QuemDa(desconto) is { } cargo
            ? $"O desconto de {FormatoBrasileiro.Porcentagem(desconto)} passa da alçada do {Alcadas.Nome(quem.Cargo)}, de {FormatoBrasileiro.Porcentagem(alcadas.De(quem.Cargo))}: precisa da aprovação do {Alcadas.Nome(cargo)}."
            : SemQuemAprove(desconto, alcadas);
    }

    /// <summary>Os problemas do conteúdo da versão, todos de uma vez.</summary>
    private static void Conferir(DadosDaVersao dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(dados.Itens);

        var problemas = dados.Itens.SelectMany((item, posicao) => ProblemasDo(item).Select(problema => $"Item {posicao + 1}: {problema}")).ToList();

        if (!Alcadas.EhPorcentagem(dados.Desconto))
        {
            problemas.Add($"O desconto vai de 0% a menos de 100%, com até duas casas: {FormatoBrasileiro.Porcentagem(dados.Desconto)} não serve.");
        }

        if (Textos.Opcional(dados.CondicoesDePagamento)?.Length > MaiorTexto)
        {
            problemas.Add($"As condições de pagamento passam de {MaiorTexto} caracteres.");
        }

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }
    }

    private static IEnumerable<string> ProblemasDo(DadosDoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Textos.Problema(item.Descricao, "a descrição", MaiorDescricao) is { } descricao)
        {
            yield return Minuscula(descricao);
        }

        if (item.Quantidade <= 0)
        {
            yield return "a quantidade precisa ser maior que zero.";
        }

        if (Textos.Problema(item.Unidade, "a unidade", MaiorUnidade) is { } unidade)
        {
            yield return Minuscula(unidade);
        }

        if (item.PrecoUnitario <= Dinheiro.Zero)
        {
            yield return "o preço unitário precisa ser maior que zero.";
        }
        else if (!item.PrecoUnitario.EstaEmCentavos)
        {
            yield return $"o preço unitário vai até o centavo: {item.PrecoUnitario} tem casas demais.";
        }
    }

    private static string Minuscula(string frase) => $"{char.ToLowerInvariant(frase[0])}{frase[1..]}";

    private void ExigirAberta()
    {
        if (Aceita is { } aceita)
        {
            throw new ExcecaoDeDominio($"A proposta foi aceita na versão {aceita.Numero}, e não muda mais.");
        }
    }

    private void ExigirRascunho()
    {
        ExigirAberta();

        if (Atual.Situacao != SituacaoDaVersao.Rascunho)
        {
            throw new ExcecaoDeDominio($"A versão {Atual.Numero} já foi enviada, e não muda mais: crie uma versão nova.");
        }
    }
}
