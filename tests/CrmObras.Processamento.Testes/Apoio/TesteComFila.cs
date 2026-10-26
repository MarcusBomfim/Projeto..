using CrmObras.Infraestrutura.Testes.Persistencia;
using CrmObras.Processamento.Planejamento;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>
/// O começo de todo teste que mexe na fila: a caixa de saída vazia, uma
/// construtora nova, um relógio parado e um planejamento de mentira no ar.
/// </summary>
/// <remarks>
/// Vazia porque a fila é uma só para o banco inteiro e reserva mensagem de
/// qualquer construtora: o que o teste reserva tem de ser o que ele mesmo
/// gravou. Pelo mesmo motivo, os testes deste assembly rodam um de cada vez.
/// </remarks>
public abstract class TesteComFila(BancoDeTestes banco) : IAsyncLifetime
{
    private PlanejamentoDeMentira? _planejamento;

    protected static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    protected BancoDeTestes Banco => banco;

    protected Guid Construtora { get; } = Guid.CreateVersion7();

    protected RelogioDeTeste Relogio { get; } = new();

    protected PlanejamentoDeMentira Planejamento =>
        _planejamento ?? throw new InvalidOperationException("O planejamento de mentira sobe antes do teste.");

    protected VendasDaConstrutora Vendas(Guid? construtora = null) => new(banco, construtora ?? Construtora, Relogio);

    protected AmbienteDoProcessador Processamento(
        Action<IServiceCollection>? registrar = null,
        Action<OpcoesDoPlanejamento>? configurarPlanejamento = null) =>
        new(banco, Relogio, Planejamento.Endereco, registrar, configurarPlanejamento);

    public async ValueTask InitializeAsync()
    {
        _planejamento = await PlanejamentoDeMentira.Subir();

        // Sem banco, o teste que precisa dele é pulado ao pedir a conexão.
        if (banco.Disponivel)
        {
            await CaixaNoBanco.Esvaziar(banco);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_planejamento is not null)
        {
            await _planejamento.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Espera a condição valer, perguntando de tempos em tempos, até o limite.</summary>
    protected static async Task Ate(Func<Task<bool>> condicao, TimeSpan limite, string descricao)
    {
        ArgumentNullException.ThrowIfNull(condicao);

        var fim = DateTime.UtcNow + limite;

        while (!await condicao())
        {
            Assert.True(DateTime.UtcNow < fim, $"Passou {limite} e ainda não: {descricao}.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), Cancelamento);
        }
    }
}
