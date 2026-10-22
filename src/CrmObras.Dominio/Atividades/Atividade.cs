using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Atividades;

/// <summary>
/// Algo a fazer com o cliente — uma ligação, uma reunião, uma visita —, com
/// data, responsável e, depois de feito, o resultado (ADR-0006).
/// </summary>
/// <remarks>
/// <para>
/// É da conta, e pode servir a uma venda aberta: é a atividade pendente que
/// diz qual é o próximo passo dela. Concluir pede o resultado — ligação sem
/// anotação não deixa nada para quem vier depois —, e cancelar, o motivo.
/// </para>
/// <para>
/// Adiar conta: cada vez que a atividade vai para mais tarde, o número de
/// adiamentos sobe. Antecipar não é adiar.
/// </para>
/// </remarks>
public sealed class Atividade
{
    private const int MaiorAssunto = 200;

    private const int MaiorResponsavel = 100;

    private const int MaiorTexto = 1000;

    /// <param name="conta">O cliente.</param>
    /// <param name="dados">O que, quando e com quem.</param>
    /// <param name="responsavel">Quem vai fazer: o login.</param>
    /// <param name="em">Quando a atividade foi marcada.</param>
    /// <param name="oportunidade">A venda a que a atividade serve, se serve a uma. Tem de ser da conta, e estar aberta.</param>
    public Atividade(Conta conta, DadosDaAtividade dados, string responsavel, DateTimeOffset em, Oportunidade? oportunidade = null)
        : this(AtividadeId.Novo(), IdDa(conta), oportunidade?.Id, dados, responsavel, em, ProblemasDoVinculo(conta, dados, oportunidade))
    {
    }

    /// <param name="doVinculo">O que está errado na ligação com a conta e a venda, conferido por quem tem a conta e a venda.</param>
    private Atividade(
        AtividadeId id,
        ContaId conta,
        OportunidadeId? oportunidade,
        DadosDaAtividade dados,
        string responsavel,
        DateTimeOffset em,
        IEnumerable<string> doVinculo)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(dados);

        List<string> problemas =
        [
            .. new[]
            {
                Textos.Problema(dados.Assunto, "o assunto da atividade", MaiorAssunto),
                Textos.Problema(responsavel, "o responsável pela atividade", MaiorResponsavel),
                Textos.Opcional(dados.Detalhes)?.Length > MaiorTexto ? $"Os detalhes passam de {MaiorTexto} caracteres." : null,
            }.OfType<string>(),
            .. doVinculo,
        ];

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Id = id;
        Conta = conta;
        Oportunidade = oportunidade;
        Tipo = dados.Tipo;
        Assunto = dados.Assunto.Trim();
        Com = dados.Com;
        Detalhes = Textos.Opcional(dados.Detalhes);
        PrevistaPara = dados.PrevistaPara;
        Responsavel = responsavel.Trim();
        CriadaEm = em;
    }

    public AtividadeId Id { get; }

    public ContaId Conta { get; }

    /// <summary>A venda a que a atividade serve, se serve a uma.</summary>
    public OportunidadeId? Oportunidade { get; }

    public TipoDeAtividade Tipo { get; }

    public string Assunto { get; }

    /// <summary>O contato do cliente com quem se vai falar.</summary>
    public ContatoId? Com { get; }

    public string? Detalhes { get; }

    public DateTimeOffset CriadaEm { get; }

    public DateTimeOffset PrevistaPara { get; private set; }

    /// <summary>Quantas vezes a atividade foi para mais tarde.</summary>
    public int Adiamentos { get; private set; }

    public string Responsavel { get; private set; }

    public SituacaoDaAtividade Situacao { get; private set; }

    /// <summary>O que aconteceu: o que foi dito, o que ficou combinado.</summary>
    public string? Resultado { get; private set; }

    public string? MotivoDoCancelamento { get; private set; }

    /// <summary>Quando foi concluída ou cancelada.</summary>
    public DateTimeOffset? EncerradaEm { get; private set; }

    /// <summary>Tudo o que a atividade é, para gravar e refazer (ADR-0010).</summary>
    internal EstadoDaAtividade Estado => new(
        Conta,
        Oportunidade,
        new DadosDaAtividade(Tipo, Assunto, PrevistaPara, Com, Detalhes),
        Responsavel,
        CriadaEm,
        Adiamentos,
        Situacao,
        Resultado,
        MotivoDoCancelamento,
        EncerradaEm);

    /// <summary>Se ainda está por fazer e a hora já passou.</summary>
    public bool EstaAtrasada(DateTimeOffset agora) => Situacao == SituacaoDaAtividade.Pendente && agora > PrevistaPara;

    /// <summary>Muda a data. Para mais tarde, conta um adiamento; para a mesma, não muda nada.</summary>
    public void Reagendar(DateTimeOffset para, DateTimeOffset em)
    {
        ExigirPendente();

        if (para <= em)
        {
            throw new ExcecaoDeDominio($"A atividade {Assunto} se reagenda para depois de agora: para antes, ela já nasceria atrasada.");
        }

        if (para > PrevistaPara)
        {
            Adiamentos++;
        }

        PrevistaPara = para;
    }

    public void TransferirPara(string responsavel)
    {
        ExigirPendente();

        if (Textos.Problema(responsavel, "o responsável pela atividade", MaiorResponsavel) is { } problema)
        {
            throw new ExcecaoDeDominio(problema);
        }

        Responsavel = responsavel.Trim();
    }

    public void Concluir(string resultado, DateTimeOffset em)
    {
        ExigirPendente();
        ExigirDepoisDeMarcada(em, "concluída");

        if (Textos.Opcional(resultado) is null)
        {
            throw new ExcecaoDeDominio($"Concluir a atividade {Assunto} pede o resultado: o que foi dito, o que ficou combinado.");
        }

        if (resultado.Trim().Length > MaiorTexto)
        {
            throw new ExcecaoDeDominio($"O resultado passa de {MaiorTexto} caracteres.");
        }

        Situacao = SituacaoDaAtividade.Concluida;
        Resultado = resultado.Trim();
        EncerradaEm = em;
    }

    public void Cancelar(string motivo, DateTimeOffset em)
    {
        ExigirPendente();
        ExigirDepoisDeMarcada(em, "cancelada");

        if (Textos.Opcional(motivo) is null)
        {
            throw new ExcecaoDeDominio($"Cancelar a atividade {Assunto} pede o motivo.");
        }

        if (motivo.Trim().Length > MaiorTexto)
        {
            throw new ExcecaoDeDominio($"O motivo passa de {MaiorTexto} caracteres.");
        }

        Situacao = SituacaoDaAtividade.Cancelada;
        MotivoDoCancelamento = motivo.Trim();
        EncerradaEm = em;
    }

    public override string ToString() => Assunto;

    /// <summary>
    /// Refaz uma atividade gravada, com a identidade dela (ADR-0010). O que se
    /// confere é o que vale sempre — os textos, e a situação coerente com o
    /// resultado, o motivo e o encerramento —, e não o que valia ao marcar: a
    /// venda pode ter fechado depois, e o contato, saído da conta.
    /// </summary>
    internal static Atividade Reconstituir(AtividadeId id, EstadoDaAtividade estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        var atividade = new Atividade(id, estado.Conta, estado.Oportunidade, estado.Dados, estado.Responsavel, estado.CriadaEm, []);
        var coerente = estado.Adiamentos >= 0 && estado.Situacao switch
        {
            SituacaoDaAtividade.Pendente => estado is { Resultado: null, MotivoDoCancelamento: null, EncerradaEm: null },
            SituacaoDaAtividade.Concluida => estado.MotivoDoCancelamento is null && TextoQueServe(estado.Resultado) && estado.EncerradaEm >= estado.CriadaEm,
            _ => estado.Resultado is null && TextoQueServe(estado.MotivoDoCancelamento) && estado.EncerradaEm >= estado.CriadaEm,
        };

        if (!coerente)
        {
            throw new ExcecaoDeDominio($"A atividade {atividade.Assunto} está gravada como {estado.Situacao}, num estado que as regras não produzem.");
        }

        atividade.Adiamentos = estado.Adiamentos;
        atividade.Situacao = estado.Situacao;
        atividade.Resultado = estado.Resultado;
        atividade.MotivoDoCancelamento = estado.MotivoDoCancelamento;
        atividade.EncerradaEm = estado.EncerradaEm;

        return atividade;
    }

    private static ContaId IdDa(Conta conta)
    {
        ArgumentNullException.ThrowIfNull(conta);

        return conta.Id;
    }

    private static IEnumerable<string> ProblemasDoVinculo(Conta conta, DadosDaAtividade dados, Oportunidade? oportunidade)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (dados.Com is { } contato && !conta.Contatos.Any(daConta => daConta.Id == contato))
        {
            yield return $"A conta {conta.Nome} não tem o contato {contato}.";
        }

        if (oportunidade is not null && oportunidade.Conta != conta.Id)
        {
            yield return $"A oportunidade {oportunidade.Titulo} não é da conta {conta.Nome}.";
        }

        if (oportunidade is { Situacao: not SituacaoDaOportunidade.Aberta })
        {
            yield return $"A oportunidade {oportunidade.Titulo} está fechada: a atividade nova é da conta, e não da venda.";
        }
    }

    /// <summary>Um resultado ou um motivo como quem encerra a atividade deixaria: sem espaço nas pontas, e dentro do tamanho.</summary>
    private static bool TextoQueServe(string? texto) => Textos.Opcional(texto) == texto && texto?.Length <= MaiorTexto;

    private void ExigirPendente()
    {
        if (Situacao != SituacaoDaAtividade.Pendente)
        {
            throw new ExcecaoDeDominio(Situacao == SituacaoDaAtividade.Concluida
                ? $"A atividade {Assunto} já foi concluída, e não muda mais."
                : $"A atividade {Assunto} foi cancelada, e não muda mais: marque outra.");
        }
    }

    private void ExigirDepoisDeMarcada(DateTimeOffset em, string comoTermina)
    {
        if (em < CriadaEm)
        {
            throw new ExcecaoDeDominio($"A atividade {Assunto} não pode ser {comoTermina} antes de ter sido marcada.");
        }
    }
}

/// <summary>Tudo o que uma atividade é, além da identidade: o que se grava, e de onde ela é refeita.</summary>
internal sealed record EstadoDaAtividade(
    ContaId Conta,
    OportunidadeId? Oportunidade,
    DadosDaAtividade Dados,
    string Responsavel,
    DateTimeOffset CriadaEm,
    int Adiamentos,
    SituacaoDaAtividade Situacao,
    string? Resultado = null,
    string? MotivoDoCancelamento = null,
    DateTimeOffset? EncerradaEm = null);
