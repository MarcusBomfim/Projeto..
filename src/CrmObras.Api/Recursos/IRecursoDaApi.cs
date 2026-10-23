namespace CrmObras.Api.Recursos;

/// <summary>
/// Um recurso da API: os endpoints de um tipo de agregado, sob o seu
/// prefixo.
/// </summary>
/// <remarks>
/// Cada recurso se mapeia sozinho, e o <see cref="MapaDosRecursos"/> encontra
/// todos: um recurso novo entra sem mexer no <c>Program</c> nem num registro
/// central. Que todo endpoint tenha política e os cabeçalhos certos, quem
/// confere são os testes da API, percorrendo os endpoints mapeados.
/// </remarks>
public interface IRecursoDaApi
{
    void Mapear(IEndpointRouteBuilder api);
}

/// <summary>Encontra os recursos da API no assembly e mapeia cada um.</summary>
internal static class MapaDosRecursos
{
    public static void MapearTodos(IEndpointRouteBuilder api)
    {
        var recursos = typeof(MapaDosRecursos).Assembly.GetTypes()
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false } && tipo.IsAssignableTo(typeof(IRecursoDaApi)))
            .OrderBy(tipo => tipo.Name, StringComparer.Ordinal);

        foreach (var tipo in recursos)
        {
            ((IRecursoDaApi)Activator.CreateInstance(tipo)!).Mapear(api);
        }
    }
}
