using CrmObras.Dominio.Comum;
using EtapaNaOrdem = (string Nome, int Probabilidade, CrmObras.Dominio.Funis.Requisitos Requisitos);

namespace CrmObras.Dominio.Funis;

/// <summary>
/// O caminho da venda, etapa por etapa, do primeiro contato até a obra
/// ganha ou perdida (ADR-0004).
/// </summary>
/// <remarks>
/// <para>
/// Cada construtora vende de um jeito — e a mesma construtora vende obra
/// pública de um, e reforma de outro. As etapas abertas são configuráveis: o
/// nome, a probabilidade de fechar, o tempo que a venda costuma ficar nelas e
/// o que a oportunidade precisa ter para entrar. A probabilidade nunca cai
/// conforme a venda avança, e a primeira etapa não tem requisito: é nela que
/// a venda nasce, sem nada.
/// </para>
/// <para>
/// No fim, sempre, a Ganha, com 100%, e a Perdida, com 0%: podem mudar de
/// nome, e só. Etapa que deixa de ser usada é arquivada, e não apagada — a
/// história das vendas que passaram por ela continua apontando para ela.
/// </para>
/// </remarks>
public sealed class Funil
{
    private const int MaiorNome = 100;

    private const int MaiorNomeDaEtapa = 60;

    private const int MaiorPrazo = 365;

    private readonly List<Etapa> _abertas = [];

    private readonly List<Etapa> _arquivadas = [];

    /// <param name="nome">Como o funil se chama: "Obras privadas".</param>
    /// <param name="etapas">As etapas abertas, na ordem da venda. A Ganha e a Perdida o funil cria.</param>
    public Funil(string nome, IReadOnlyList<DadosDaEtapa> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        var problemas = new List<string>();

        if (Textos.Problema(nome, "o nome do funil", MaiorNome) is { } problema)
        {
            problemas.Add(problema);
        }

        if (etapas.Count == 0)
        {
            problemas.Add("O funil precisa de uma etapa aberta, ao menos: é nela que a venda começa.");
        }

        var nomes = new List<string>();

        foreach (var dados in etapas)
        {
            problemas.AddRange(ProblemasDa(dados));

            if (Textos.Opcional(dados.Nome) is { } nomeDaEtapa)
            {
                if (MesmoNome(nomeDaEtapa, "Ganha") || MesmoNome(nomeDaEtapa, "Perdida"))
                {
                    problemas.Add($"{nomeDaEtapa} é etapa final, e o funil já a tem: a lista é só das etapas abertas.");
                }
                else if (nomes.Exists(outro => MesmoNome(outro, nomeDaEtapa)))
                {
                    problemas.Add($"A etapa {nomeDaEtapa} aparece duas vezes.");
                }

                nomes.Add(nomeDaEtapa);
            }
        }

        if (problemas.Count == 0)
        {
            problemas.AddRange(ProblemasDaOrdem([.. etapas.Select(NaOrdem)]));
        }

        Lancar(problemas);

        Id = FunilId.Novo();
        Nome = nome.Trim();
        _abertas.AddRange(etapas.Select(dados => new Etapa(EtapaId.Novo(), TipoDeEtapa.Aberta, dados)));
        Ganha = new Etapa(EtapaId.Novo(), TipoDeEtapa.Ganha, new DadosDaEtapa("Ganha", 100));
        Perdida = new Etapa(EtapaId.Novo(), TipoDeEtapa.Perdida, new DadosDaEtapa("Perdida", 0));
    }

    public FunilId Id { get; }

    public string Nome { get; private set; }

    /// <summary>As etapas abertas em uso, na ordem da venda.</summary>
    public IReadOnlyList<Etapa> Abertas => _abertas;

    /// <summary>As etapas que saíram do funil, na ordem em que saíram.</summary>
    public IReadOnlyList<Etapa> Arquivadas => _arquivadas;

    /// <summary>Onde toda oportunidade começa.</summary>
    public Etapa Primeira => _abertas[0];

    public Etapa Ganha { get; }

    public Etapa Perdida { get; }

    /// <summary>
    /// O funil de quem vende obra a empresa: do primeiro contato à
    /// negociação, passando pela visita técnica e pelo orçamento.
    /// </summary>
    /// <remarks>
    /// Orçamento só com quem decide identificado: orçar para quem não decide é
    /// trabalho perdido. Proposta só com valor e previsão de fechamento: é a
    /// partir dela que a venda conta na previsão.
    /// </remarks>
    public static Funil PadraoDaConstrutora() => new(
        "Vendas de obras",
        [
            new("Primeiro contato", 10, DiasEsperados: 7),
            new("Visita técnica", 20, DiasEsperados: 14),
            new("Orçamento em elaboração", 40, DiasEsperados: 21, Requisitos.QuemDecide),
            new("Proposta enviada", 60, DiasEsperados: 15, Requisitos.ValorEstimado | Requisitos.PrevisaoDeFechamento),
            new("Negociação", 80, DiasEsperados: 30),
        ]);

    public void Renomear(string nome)
    {
        if (Textos.Problema(nome, "o nome do funil", MaiorNome) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Nome = nome.Trim();
    }

    /// <summary>
    /// Acrescenta uma etapa aberta, no lugar que a probabilidade indica:
    /// depois das etapas com probabilidade igual ou menor.
    /// </summary>
    public Etapa AdicionarEtapa(DadosDaEtapa dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        List<string> problemas = [.. ProblemasDa(dados), .. NomeRepetido(dados.Nome, alterada: null)];
        var posicao = PosicaoPara(dados.Probabilidade);

        if (problemas.Count == 0)
        {
            List<EtapaNaOrdem> ordem = [.. _abertas.Select(NaOrdem)];
            ordem.Insert(posicao, NaOrdem(dados));
            problemas.AddRange(ProblemasDaOrdem(ordem));
        }

        Lancar(problemas);

        var etapa = new Etapa(EtapaId.Novo(), TipoDeEtapa.Aberta, dados);
        _abertas.Insert(posicao, etapa);

        return etapa;
    }

    /// <summary>Muda o nome, a probabilidade, o prazo e os requisitos de uma etapa aberta, sem mudá-la de lugar.</summary>
    public void AlterarEtapa(EtapaId id, DadosDaEtapa dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        var etapa = Etapa(id);

        if (etapa.EhFinal)
        {
            throw new ExcecaoDeDominio(
                $"{etapa.Nome} é etapa final: a probabilidade é sempre {etapa.Probabilidade}%, sem prazo nem requisito, e só o nome muda.");
        }

        List<string> problemas = [.. ProblemasDa(dados), .. NomeRepetido(dados.Nome, etapa)];

        if (problemas.Count == 0 && !etapa.Arquivada)
        {
            problemas.AddRange(ProblemasDaOrdem([.. _abertas.Select(aberta => aberta == etapa ? NaOrdem(dados) : NaOrdem(aberta))]));
        }

        Lancar(problemas);
        etapa.Alterar(dados);
    }

    /// <summary>Muda o nome de qualquer etapa, a Ganha e a Perdida inclusive.</summary>
    public void RenomearEtapa(EtapaId id, string nome)
    {
        var etapa = Etapa(id);

        if (Textos.Problema(nome, "o nome da etapa", MaiorNomeDaEtapa) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Lancar([.. NomeRepetido(nome, etapa)]);
        etapa.Renomear(nome);
    }

    /// <summary>Leva uma etapa aberta para outra posição — a primeira é a 1 —, se a probabilidade continuar sem cair.</summary>
    public void MoverEtapa(EtapaId id, int posicao)
    {
        var etapa = Etapa(id);

        if (etapa.EhFinal)
        {
            throw new ExcecaoDeDominio($"{etapa.Nome} é etapa final, e fica sempre no fim do funil.");
        }

        if (etapa.Arquivada)
        {
            throw new ExcecaoDeDominio($"A etapa {etapa.Nome} está arquivada: reative-a antes de mover.");
        }

        if (posicao < 1 || posicao > _abertas.Count)
        {
            throw new ExcecaoDeDominio(
                $"Não há a posição {posicao}: as etapas abertas do funil {Nome} vão da 1 à {_abertas.Count}.");
        }

        List<Etapa> ordem = [.. _abertas.Where(aberta => aberta != etapa)];
        ordem.Insert(posicao - 1, etapa);
        Lancar([.. ProblemasDaOrdem([.. ordem.Select(NaOrdem)])]);

        _abertas.Clear();
        _abertas.AddRange(ordem);
    }

    /// <summary>Tira a etapa do funil: nenhuma oportunidade entra mais nela. Arquivar a arquivada não muda nada.</summary>
    public void ArquivarEtapa(EtapaId id)
    {
        var etapa = Etapa(id);

        if (etapa.EhFinal)
        {
            throw new ExcecaoDeDominio($"{etapa.Nome} é etapa final, e não é arquivada: toda venda termina ganha ou perdida.");
        }

        if (etapa.Arquivada)
        {
            return;
        }

        if (_abertas.Count == 1)
        {
            throw new ExcecaoDeDominio(
                $"{etapa.Nome} é a única etapa aberta do funil {Nome}: crie outra antes de arquivá-la, porque é nela que a venda começa.");
        }

        Lancar([.. ProblemasDaOrdem([.. _abertas.Where(aberta => aberta != etapa).Select(NaOrdem)])]);

        _abertas.Remove(etapa);
        _arquivadas.Add(etapa);
        etapa.Arquivar();
    }

    /// <summary>Devolve ao funil a etapa arquivada, no lugar que a probabilidade dela indica.</summary>
    public void ReativarEtapa(EtapaId id)
    {
        var etapa = Etapa(id);

        if (!etapa.Arquivada)
        {
            throw new ExcecaoDeDominio($"A etapa {etapa.Nome} não está arquivada: não há o que reativar.");
        }

        var posicao = PosicaoPara(etapa.Probabilidade);
        List<EtapaNaOrdem> ordem = [.. _abertas.Select(NaOrdem)];
        ordem.Insert(posicao, NaOrdem(etapa));
        Lancar([.. ProblemasDaOrdem(ordem)]);

        _arquivadas.Remove(etapa);
        _abertas.Insert(posicao, etapa);
        etapa.Reativar();
    }

    /// <summary>Qualquer etapa do funil, a arquivada inclusive.</summary>
    public Etapa Etapa(EtapaId id) =>
        Todas().FirstOrDefault(etapa => etapa.Id == id)
        ?? throw new ExcecaoDeDominio($"O funil {Nome} não tem a etapa {id}.");

    /// <summary>
    /// O que a oportunidade precisa ter para entrar na etapa: os requisitos
    /// dela e os de todas as abertas antes dela. Pular etapa não pula a
    /// exigência.
    /// </summary>
    /// <remarks>
    /// A Ganha e a Perdida não têm requisitos de etapa: ganhar pede o valor
    /// fechado, e perder, o motivo.
    /// </remarks>
    public Requisitos RequisitosPara(EtapaId id)
    {
        var etapa = Etapa(id);

        if (etapa.Arquivada)
        {
            throw new ExcecaoDeDominio($"A etapa {etapa.Nome} está arquivada: nenhuma oportunidade entra nela.");
        }

        return etapa.EhFinal
            ? Requisitos.Nenhum
            : _abertas
                .Take(_abertas.IndexOf(etapa) + 1)
                .Aggregate(Requisitos.Nenhum, (requisitos, anterior) => requisitos | anterior.Requisitos);
    }

    public override string ToString() => Nome;

    private static bool MesmoNome(string um, string outro) => string.Equals(um, outro, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ProblemasDa(DadosDaEtapa dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (Textos.Problema(dados.Nome, "o nome da etapa", MaiorNomeDaEtapa) is { } problema)
        {
            yield return problema;
        }

        var daEtapa = Textos.Opcional(dados.Nome) is { } nome ? $"da etapa {nome}" : "da etapa";

        if (dados.Probabilidade is < 1 or > 99)
        {
            yield return $"A probabilidade {daEtapa} vai de 1% a 99%: 0% é perder, e 100%, ganhar.";
        }

        if (dados.DiasEsperados is < 1 or > MaiorPrazo)
        {
            yield return $"O tempo esperado {daEtapa} vai de 1 a {MaiorPrazo} dias.";
        }
    }

    private static EtapaNaOrdem NaOrdem(DadosDaEtapa dados) => (dados.Nome.Trim(), dados.Probabilidade, dados.Requisitos);

    private static EtapaNaOrdem NaOrdem(Etapa etapa) => (etapa.Nome, etapa.Probabilidade, etapa.Requisitos);

    /// <summary>
    /// O que uma nova ordem das etapas abertas quebraria: requisito na
    /// primeira, probabilidade que cai de uma etapa para a seguinte.
    /// </summary>
    private static IEnumerable<string> ProblemasDaOrdem(List<EtapaNaOrdem> ordem)
    {
        if (ordem[0].Requisitos != Requisitos.Nenhum)
        {
            yield return $"{ordem[0].Nome} ficaria em primeiro, e a primeira etapa não tem requisito: é nela que a venda nasce, sem nada.";
        }

        foreach (var (anterior, seguinte) in ordem.Zip(ordem.Skip(1)))
        {
            if (seguinte.Probabilidade < anterior.Probabilidade)
            {
                yield return $"{seguinte.Nome} ({seguinte.Probabilidade}%) não pode vir depois de {anterior.Nome} ({anterior.Probabilidade}%): a probabilidade não cai conforme a venda avança.";
            }
        }
    }

    private static void Lancar(List<string> problemas)
    {
        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }
    }

    private IEnumerable<Etapa> Todas() => _abertas.Concat(_arquivadas).Append(Ganha).Append(Perdida);

    private IEnumerable<string> NomeRepetido(string? nome, Etapa? alterada)
    {
        if (Textos.Opcional(nome) is not { } procurado)
        {
            yield break;
        }

        var outra = Todas().FirstOrDefault(etapa => etapa != alterada && MesmoNome(etapa.Nome, procurado));

        if (outra is not null)
        {
            yield return outra.Arquivada
                ? $"Já existe a etapa {outra.Nome}, arquivada: reative-a, ou escolha outro nome."
                : $"Já existe a etapa {outra.Nome}.";
        }
    }

    private int PosicaoPara(int probabilidade) => _abertas.FindLastIndex(etapa => etapa.Probabilidade <= probabilidade) + 1;
}
