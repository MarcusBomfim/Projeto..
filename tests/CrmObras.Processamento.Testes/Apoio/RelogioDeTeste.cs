namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>Um relógio que o teste acerta e adianta: a fila só entrega o que já está na hora.</summary>
public sealed class RelogioDeTeste : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Adiantar(TimeSpan quanto) => Agora += quanto;
}
