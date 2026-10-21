namespace CrmObras.Dominio.Funis;

/// <summary>Como se configura uma etapa aberta do funil — para criar o funil, para adicionar, para alterar.</summary>
/// <param name="Nome">Como a etapa aparece no quadro: "Visita técnica".</param>
/// <param name="Probabilidade">A chance de fechar a venda que está na etapa, em porcentagem: de 1 a 99.</param>
/// <param name="DiasEsperados">Quantos dias a venda costuma ficar na etapa; passado isso, está parada. Nulo se não há prazo.</param>
/// <param name="Requisitos">O que a oportunidade precisa ter para entrar na etapa — e em qualquer uma depois dela.</param>
public sealed record DadosDaEtapa(
    string Nome,
    int Probabilidade,
    int? DiasEsperados = null,
    Requisitos Requisitos = Requisitos.Nenhum);

/// <summary>Uma coluna do funil: em que ponto a venda está.</summary>
/// <remarks>
/// Só muda pelo funil dela: é o funil que sabe se o nome já é de outra
/// etapa, ou se a probabilidade cabe entre a da etapa anterior e a da
/// seguinte.
/// </remarks>
public sealed class Etapa
{
    internal Etapa(EtapaId id, TipoDeEtapa tipo, DadosDaEtapa dados)
    {
        Id = id;
        Tipo = tipo;
        Nome = string.Empty;
        Alterar(dados);
    }

    public EtapaId Id { get; }

    public TipoDeEtapa Tipo { get; }

    public string Nome { get; private set; }

    /// <summary>A chance de fechar, em porcentagem: 100 na Ganha, 0 na Perdida.</summary>
    public int Probabilidade { get; private set; }

    public int? DiasEsperados { get; private set; }

    /// <summary>Os requisitos desta etapa só. Para entrar nela, valem também os das anteriores: ver <see cref="Funil.RequisitosPara"/>.</summary>
    public Requisitos Requisitos { get; private set; }

    /// <summary>Saiu do funil: nenhuma oportunidade entra mais nela, mas a história de quem passou por ela fica.</summary>
    public bool Arquivada { get; private set; }

    /// <summary>Ganha ou Perdida: a venda terminou.</summary>
    public bool EhFinal => Tipo != TipoDeEtapa.Aberta;

    public override string ToString() => Nome;

    internal void Alterar(DadosDaEtapa dados)
    {
        Nome = dados.Nome.Trim();
        Probabilidade = dados.Probabilidade;
        DiasEsperados = dados.DiasEsperados;
        Requisitos = dados.Requisitos;
    }

    internal void Renomear(string nome) => Nome = nome.Trim();

    internal void Arquivar() => Arquivada = true;

    internal void Reativar() => Arquivada = false;
}
