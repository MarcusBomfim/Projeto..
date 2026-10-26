namespace CrmObras.Dominio.Comum;

/// <summary>
/// Um fato do domínio, no passado: algo que aconteceu com um agregado e que
/// outras partes do sistema — ou outros sistemas — podem querer saber
/// (ADR-0016).
/// </summary>
/// <remarks>
/// O fato é o que aconteceu naquele instante, e não muda depois: a venda
/// ganha leva o título e o valor de quando foi ganha, mesmo que o título
/// mude amanhã. Quem trata o fato recebe sempre o mesmo, em quantas
/// tentativas forem precisas.
/// </remarks>
public abstract record EventoDeDominio
{
    /// <summary>O agregado com que o fato aconteceu.</summary>
    public abstract Identificador Agregado { get; }
}

/// <summary>
/// Um agregado que registra os fatos que acontecem com ele, na ordem em que
/// acontecem. A persistência os publica junto com a gravação, e os descarta.
/// </summary>
public abstract class AgregadoComEventos
{
    private readonly List<EventoDeDominio> _eventos = [];

    /// <summary>Os fatos desde a última gravação, do mais antigo ao mais novo.</summary>
    public IReadOnlyList<EventoDeDominio> Eventos => _eventos.AsReadOnly();

    protected void Registrar(EventoDeDominio evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        _eventos.Add(evento);
    }

    /// <summary>Esquece os eventos, depois que a persistência os publicou junto com o agregado.</summary>
    internal void DescartarEventos() => _eventos.Clear();
}
