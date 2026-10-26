using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;

namespace CrmObras.Processamento.Hospedagem;

/// <summary>
/// O aviso de mensagem nova, pelo <c>LISTEN</c>/<c>NOTIFY</c> do PostgreSQL:
/// o processador acorda assim que uma mensagem é gravada, em vez de esperar a
/// próxima volta (ADR-0016).
/// </summary>
/// <remarks>
/// <para>
/// Uma conexão só para escutar, fora do pool. Se ela cair, a espera vira o
/// intervalo inteiro — o processador continua, só mais devagar — e a escuta
/// é refeita na volta seguinte.
/// </para>
/// <para>
/// Abrir a escuta não deixa rastro: é olhar a fila, como a reserva, e não
/// conta história nenhuma (ADR-0018).
/// </para>
/// </remarks>
public sealed partial class EsperaPorMensagens(string conexao, ILogger<EsperaPorMensagens> registro) : IAsyncDisposable
{
    public const string Canal = "crm_caixa_de_saida";

    private readonly string _conexaoDeEscuta = new NpgsqlConnectionStringBuilder(conexao) { Pooling = false }.ConnectionString;

    private NpgsqlConnection? _escuta;

    /// <summary>Começa a escutar antes da primeira volta, para não perder aviso entre a volta e a espera.</summary>
    public async Task Preparar(CancellationToken cancelamento)
    {
        try
        {
            await Escutar(cancelamento);
        }
        catch (NpgsqlException erro)
        {
            EscutaCaiu(registro, erro);
            await Descartar();
        }
    }

    /// <summary>Espera um aviso ou o fim do intervalo, o que vier primeiro.</summary>
    /// <returns>Se acordou por aviso.</returns>
    public async Task<bool> Aguardar(TimeSpan intervalo, CancellationToken cancelamento)
    {
        try
        {
            var escuta = await Escutar(cancelamento);

            return await escuta.WaitAsync(intervalo, cancelamento);
        }
        catch (Exception erro) when (erro is NpgsqlException or InvalidOperationException or IOException)
        {
            EscutaCaiu(registro, erro);
            await Descartar();
            await Task.Delay(intervalo, cancelamento);

            return false;
        }
    }

    public ValueTask DisposeAsync() => Descartar();

    private async Task<NpgsqlConnection> Escutar(CancellationToken cancelamento)
    {
        if (_escuta is { State: ConnectionState.Open } aberta)
        {
            return aberta;
        }

        await Descartar();

        using var silencio = SuppressInstrumentationScope.Begin();
        var nova = new NpgsqlConnection(_conexaoDeEscuta);
        await nova.OpenAsync(cancelamento);

        await using (var comando = new NpgsqlCommand($"listen {Canal}", nova))
        {
            await comando.ExecuteNonQueryAsync(cancelamento);
        }

        _escuta = nova;

        return nova;
    }

    private async ValueTask Descartar()
    {
        if (_escuta is not null)
        {
            await _escuta.DisposeAsync();
            _escuta = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A escuta de mensagens novas caiu; a fila segue pelo intervalo até ela voltar.")]
    private static partial void EscutaCaiu(ILogger registro, Exception erro);
}
