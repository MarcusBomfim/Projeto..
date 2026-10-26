using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Processamento;

/// <summary>
/// Quem trata um tipo de mensagem da caixa de saída. Um tipo pode ter vários
/// tratadores, e um tratador vários tipos.
/// </summary>
/// <remarks>
/// A entrega é "ao menos uma vez": a mesma mensagem pode chegar de novo — o
/// processador caiu depois de tratar e antes de confirmar —, e pode chegar
/// depois de uma mais nova. O tratamento tem de dar o mesmo resultado nos
/// dois casos (ADR-0017).
/// </remarks>
public interface ITratadorDe<in TMensagem>
    where TMensagem : class
{
    Task Tratar(TMensagem mensagem, CancellationToken cancelamento);
}

/// <summary>Entrega a mensagem a todos os tratadores do tipo dela, no escopo da transação que a reservou.</summary>
internal static class Despachante
{
    private static readonly MethodInfo TratarComTodosGenerico =
        typeof(Despachante).GetMethod(nameof(TratarComTodos), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task>> PorTipo = new();

    public static Task Despachar(IServiceProvider servicos, object mensagem, CancellationToken cancelamento) =>
        PorTipo.GetOrAdd(
            mensagem.GetType(),
            tipo => TratarComTodosGenerico.MakeGenericMethod(tipo).CreateDelegate<Func<IServiceProvider, object, CancellationToken, Task>>())(
            servicos,
            mensagem,
            cancelamento);

    private static async Task TratarComTodos<TMensagem>(IServiceProvider servicos, object mensagem, CancellationToken cancelamento)
        where TMensagem : class
    {
        foreach (var tratador in servicos.GetServices<ITratadorDe<TMensagem>>())
        {
            await tratador.Tratar((TMensagem)mensagem, cancelamento);
        }
    }
}
