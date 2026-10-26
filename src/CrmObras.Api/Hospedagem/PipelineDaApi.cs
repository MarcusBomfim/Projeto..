using CrmObras.Api.Http;
using CrmObras.Api.Recursos;
using CrmObras.Observabilidade;

namespace CrmObras.Api.Hospedagem;

/// <summary>O caminho de cada pedido, na ordem.</summary>
internal static class PipelineDaApi
{
    public static WebApplication UsarApi(this WebApplication app)
    {
        // Primeiro, quem transforma erro em ProblemDetails: exceção de
        // qualquer etapa adiante, e resposta de erro sem corpo — o 401 da
        // autenticação, o 403 da autorização, o 404 de rota.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(Idempotencia.GuardarCorpo);

        app.MapOpenApi().AllowAnonymous();
        app.MapearSaude();
        MapaDosRecursos.MapearTodos(app.MapGroup("/api"));

        return app;
    }
}
