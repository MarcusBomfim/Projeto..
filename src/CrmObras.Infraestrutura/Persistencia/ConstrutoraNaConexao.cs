using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// Diz ao PostgreSQL, a cada conexão aberta, em nome de que construtora ela
/// trabalha. É o parâmetro que as políticas de segurança por linha leem
/// (ADR-0011).
/// </summary>
/// <remarks>
/// Toda abertura define o parâmetro — inclusive para nenhuma construtora,
/// com o valor vazio. Uma conexão que volta ao pool nunca leva a construtora
/// de um pedido para o pedido seguinte.
/// </remarks>
internal sealed class ConstrutoraNaConexao : DbConnectionInterceptor
{
    public static ConstrutoraNaConexao Instancia { get; } = new();

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var comando = DefinirConstrutora(connection, eventData);
        comando.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var comando = DefinirConstrutora(connection, eventData);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand DefinirConstrutora(DbConnection conexao, ConnectionEndEventData dados)
    {
        var construtora = (dados.Context as ContextoDoBanco)?.ConstrutoraId;

        var comando = conexao.CreateCommand();
        comando.CommandText = "select set_config('crm.construtora_id', @construtora, false)";

        var parametro = comando.CreateParameter();
        parametro.ParameterName = "construtora";
        parametro.Value = construtora?.ToString() ?? string.Empty;
        comando.Parameters.Add(parametro);

        return comando;
    }
}
