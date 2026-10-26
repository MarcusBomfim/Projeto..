using System.Collections;
using System.Diagnostics;

namespace CrmObras.Api.Testes;

/// <summary>
/// Onde o exportador em memória põe os rastros. Com trava: o SDK escuta o
/// processo inteiro, e os pedidos dos outros testes, em paralelo, continuam
/// chegando enquanto este lê os seus.
/// </summary>
public sealed class RastrosExportados : ICollection<Activity>
{
    private readonly List<Activity> _rastros = [];

    public int Count
    {
        get
        {
            lock (_rastros)
            {
                return _rastros.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    /// <summary>Os de um rastro só: os do teste que o abriu.</summary>
    public IReadOnlyList<Activity> Do(ActivityTraceId rastro)
    {
        lock (_rastros)
        {
            return [.. _rastros.Where(atividade => atividade.TraceId == rastro)];
        }
    }

    /// <summary>
    /// Os do rastro, quando todos os nomes pedidos tiverem chegado — ou o que
    /// chegou em cinco segundos. O pedido HTTP termina no servidor depois de
    /// o cliente ter a resposta na mão: a atividade dele chega um instante
    /// depois.
    /// </summary>
    public async Task<IReadOnlyList<Activity>> Esperar(ActivityTraceId rastro, params string[] nomes)
    {
        var limite = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (true)
        {
            var doRastro = Do(rastro);

            if (nomes.All(nome => doRastro.Any(atividade => atividade.DisplayName == nome)) || DateTime.UtcNow > limite)
            {
                return doRastro;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    public void Add(Activity item)
    {
        lock (_rastros)
        {
            _rastros.Add(item);
        }
    }

    public void Clear()
    {
        lock (_rastros)
        {
            _rastros.Clear();
        }
    }

    public bool Contains(Activity item)
    {
        lock (_rastros)
        {
            return _rastros.Contains(item);
        }
    }

    public void CopyTo(Activity[] array, int arrayIndex)
    {
        lock (_rastros)
        {
            _rastros.CopyTo(array, arrayIndex);
        }
    }

    public bool Remove(Activity item)
    {
        lock (_rastros)
        {
            return _rastros.Remove(item);
        }
    }

    public IEnumerator<Activity> GetEnumerator()
    {
        lock (_rastros)
        {
            return _rastros.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
