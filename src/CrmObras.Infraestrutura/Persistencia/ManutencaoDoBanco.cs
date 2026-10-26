using Microsoft.EntityFrameworkCore;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// A limpeza do que perdeu a serventia: chaves de idempotência vencidas e
/// mensagens já tratadas (ADR-0013, ADR-0016).
/// </summary>
/// <remarks>
/// A limpeza atravessa construtoras, e por isso passa por funções do banco
/// que fazem só isto — apagar o que é anterior a uma data —, como a reserva
/// da fila. Mensagem abandonada não sai: espera alguém olhar o porquê.
/// </remarks>
public sealed class ManutencaoDoBanco(ContextoDoBanco contexto)
{
    /// <returns>Quantas chaves foram apagadas.</returns>
    public Task<int> ApagarChavesDeIdempotenciaAntesDe(DateTimeOffset limite, CancellationToken cancelamento = default) =>
        contexto.Database
            .SqlQuery<int>($"select crm.apagar_chaves_de_idempotencia_antes_de({limite.ToUniversalTime()}) as \"Value\"")
            .SingleAsync(cancelamento);

    /// <returns>Quantas mensagens foram apagadas.</returns>
    public Task<int> ApagarMensagensTratadasAntesDe(DateTimeOffset limite, CancellationToken cancelamento = default) =>
        contexto.Database
            .SqlQuery<int>($"select crm.apagar_mensagens_tratadas_antes_de({limite.ToUniversalTime()}) as \"Value\"")
            .SingleAsync(cancelamento);
}
