using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CrmObras.Processamento.Testes.Apoio;

/// <summary>Guarda o que o processamento registrou, para o teste conferir.</summary>
public sealed class RegistroEmMemoria : ILoggerProvider
{
    private readonly ConcurrentQueue<Entrada> _entradas = new();

    private readonly LoggerFactory _fabrica;

    public RegistroEmMemoria() => _fabrica = new LoggerFactory([this], new LoggerFilterOptions { MinLevel = LogLevel.Debug });

    public IReadOnlyList<Entrada> Entradas => [.. _entradas];

    /// <summary>O registro de uma classe, para quem é montado fora da injeção de dependência.</summary>
    public ILogger<T> Para<T>() => _fabrica.CreateLogger<T>();

    public int Quantas(string comeco) =>
        _entradas.Count(entrada => entrada.Mensagem.StartsWith(comeco, StringComparison.Ordinal));

    public ILogger CreateLogger(string categoryName) => new Registrador(categoryName, _entradas);

    public void Dispose() => _fabrica.Dispose();

    public sealed record Entrada(string Categoria, LogLevel Nivel, string Mensagem);

    private sealed class Registrador(string categoria, ConcurrentQueue<Entrada> entradas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entradas.Enqueue(new Entrada(categoria, logLevel, formatter(state, exception)));
    }
}
