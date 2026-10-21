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
    {
        ArgumentNullException.ThrowIfNull(conta);
        ArgumentNullException.ThrowIfNull(dados);

        var problemas = new[]
        {
            Textos.Problema(dados.Assunto, "o assunto da atividade", MaiorAssunto),
            Textos.Problema(responsavel, "o responsável pela atividade", MaiorResponsavel),
            Textos.Opcional(dados.Detalhes)?.Length > MaiorTexto ? $"Os detalhes passam de {MaiorTexto} caracteres." : null,
            dados.Com is { } contato && !conta.Contatos.Any(daConta => daConta.Id == contato)
                ? $"A conta {conta.Nome} não tem o contato {contato}."
                : null,
            oportunidade is not null && oportunidade.Conta != conta.Id
                ? $"A oportunidade {oportunidade.Titulo} não é da conta {conta.Nome}."
                : null,
            oportunidade is { Situacao: not SituacaoDaOportunidade.Aberta }
                ? $"A oportunidade {oportunidade.Titulo} está fechada: a atividade nova é da conta, e não da venda."
                : null,
        }.OfType<string>().ToList();

        if (problemas.Count > 0)
        {
            throw new ExcecaoDeDominio(string.Join(" ", problemas));
        }

        Id = AtividadeId.Novo();
        Conta = conta.Id;
        Oportunidade = oportunidade?.Id;
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
