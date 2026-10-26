using System.Data.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CrmObras.Observabilidade;

/// <summary>O banco responde: abre uma conexão e roda <c>select 1</c>.</summary>
/// <remarks>
/// A descrição da falha é só esta frase. O erro de verdade — host, porta,
/// usuário — vai para o log, e não para quem pergunta pela saúde.
/// </remarks>
internal sealed class BancoNoAr(string conexao) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var aberta = new NpgsqlConnection(conexao);
            await aberta.OpenAsync(cancellationToken);

            await using var comando = new NpgsqlCommand("select 1", aberta);
            await comando.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (DbException erro)
        {
            return HealthCheckResult.Unhealthy("O banco não respondeu.", erro);
        }
    }
}
