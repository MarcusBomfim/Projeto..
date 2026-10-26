using System.Collections.Concurrent;
using CrmObras.Infraestrutura.Mensagens;
using CrmObras.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmObras.Processamento.Testes.Apoio;

/*
 * Tratadores que só existem nos testes, para ver o processador por dentro:
 * um que anota o que recebeu e em nome de quem, um que grava no banco, um
 * que falha, um que demora, um que esgota o tempo, um que para o host.
 * Entram depois dos tratadores do processamento — o que abre a obra no
 * planejamento trata antes deles.
 */

public static class TratadoresDeTeste
{
    /// <summary>Registra o tratador para todas as mensagens que ele trata, depois dos que já estão registrados.</summary>
    public static IServiceCollection TratarTambemCom<TTratador>(this IServiceCollection servicos)
        where TTratador : class
    {
        foreach (var contrato in typeof(TTratador).GetInterfaces()
                     .Where(contrato => contrato.IsGenericType && contrato.GetGenericTypeDefinition() == typeof(ITratadorDe<>)))
        {
            servicos.AddScoped(contrato, typeof(TTratador));
        }

        return servicos;
    }
}

/// <summary>Uma mensagem recebida, com a construtora em nome de quem foi tratada — no código e no banco.</summary>
public sealed record Anotacao(MensagemDeVendaGanha Mensagem, Guid? Construtora, string ConstrutoraNoBanco);

/// <summary>O que os tratadores que anotam receberam, na ordem em que receberam.</summary>
public sealed class Anotacoes
{
    private readonly ConcurrentQueue<Anotacao> _anotacoes = new();

    public IReadOnlyList<Anotacao> Todas => [.. _anotacoes];

    public void Anotar(Anotacao anotacao) => _anotacoes.Enqueue(anotacao);
}

public sealed class TratadorQueAnota(Anotacoes anotacoes, IConstrutoraAtual construtora, ContextoDoBanco contexto) : ITratadorDe<MensagemDeVendaGanha>
{
    public async Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento)
    {
        // A construtora que a segurança por linha vê, na transação do tratamento.
        var noBanco = await contexto.Database
            .SqlQuery<string>($"select current_setting('crm.construtora_id') as \"Value\"")
            .SingleAsync(cancelamento);

        anotacoes.Anotar(new Anotacao(mensagem, construtora.Id, noBanco));
    }
}

/// <summary>
/// Grava no banco, pela transação do tratamento: renomeia a venda da mensagem.
/// É o que mostra que o efeito do tratador e a marca de tratada ficam juntos.
/// </summary>
public sealed class TratadorQueGrava(ContextoDoBanco contexto, TimeProvider relogio) : ITratadorDe<MensagemDeVendaGanha>
{
    public const string Marca = " (obra aberta)";

    public async Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento)
    {
        var oportunidades = new Infraestrutura.Persistencia.Repositorios.RepositorioDeOportunidades(contexto, relogio);
        var lida = (await oportunidades.Obter(new Dominio.Oportunidades.OportunidadeId(mensagem.Oportunidade), cancelamento))!;
        lida.Agregado.Renomear(lida.Agregado.Titulo + Marca);

        await oportunidades.Atualizar(lida.Agregado, lida.Versao, cancelamento);
    }
}

/// <summary>Quantas vezes o tratador que falha ainda vai falhar.</summary>
public sealed class Falhas(int vezes)
{
    private int _restantes = vezes;

    public bool Falhar() => Interlocked.Decrement(ref _restantes) >= 0;
}

public sealed class TratadorQueFalha(Falhas falhas) : ITratadorDe<MensagemDeVendaGanha>
{
    public Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento) =>
        falhas.Falhar() ? throw new InvalidOperationException("Falha de propósito.") : Task.CompletedTask;
}

/// <summary>Segura a mensagem reservada um pouco: dá tempo de outro processador tentar pegá-la.</summary>
public sealed class TratadorDemorado : ITratadorDe<MensagemDeVendaGanha>
{
    public Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento) =>
        Task.Delay(TimeSpan.FromMilliseconds(20), cancelamento);
}

/// <summary>O host parando no meio do tratamento.</summary>
public sealed class TratadorQueCancela(CancellationTokenSource parar) : ITratadorDe<MensagemDeVendaGanha>
{
    public async Task Tratar(MensagemDeVendaGanha mensagem, CancellationToken cancelamento)
    {
        await parar.CancelAsync();
        cancelamento.ThrowIfCancellationRequested();
    }
}
