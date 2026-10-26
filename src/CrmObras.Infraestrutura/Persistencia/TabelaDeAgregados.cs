using System.Diagnostics;
using System.Text.Json;
using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Persistencia.CaixaDeSaida;
using CrmObras.Infraestrutura.Persistencia.Registros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// O que todo repositório faz com a sua tabela: ler, inserir, atualizar com
/// concorrência otimista, apagar — e traduzir o que o PostgreSQL responde
/// para as exceções da persistência.
/// </summary>
/// <remarks>
/// <para>
/// Atualizar e apagar são um comando SQL só, com a versão no <c>where</c>:
/// <c>update ... where id = @id and versao = @lida</c>. Se outro gravou no
/// meio, nenhuma linha muda, e aí uma consulta separa "mudou" de "sumiu". Não
/// há janela entre conferir a versão e gravar (ADR-0012).
/// </para>
/// <para>
/// Nada fica no rastreador de mudanças do Entity Framework: leitura sem
/// rastreio, atualização e remoção direto no banco. O contexto não guarda
/// estado entre uma operação e outra.
/// </para>
/// </remarks>
/// <param name="agregado">Como o agregado se chama numa frase: "a conta", "o funil".</param>
internal sealed class TabelaDeAgregados<TRegistro>(ContextoDoBanco contexto, TimeProvider relogio, string agregado)
    where TRegistro : RegistroDeAgregado
{
    private DbSet<TRegistro> Registros => contexto.Set<TRegistro>();

    public DateTimeOffset Agora => relogio.GetUtcNow();

    public async Task<Versionado<T>?> Obter<T>(Guid id, Func<TRegistro, T> leitura, CancellationToken cancelamento)
    {
        var registro = await Registros.AsNoTracking().SingleOrDefaultAsync(registro => registro.Id == id, cancelamento);

        return registro is null ? null : new Versionado<T>(Abrir(registro, leitura), registro.Versao);
    }

    /// <summary>Os agregados que a consulta escolhe — com LINQ, ou SQL que se compõe —, cada um com a versão em que estava.</summary>
    public async Task<IReadOnlyList<Versionado<T>>> Consultar<T>(
        Func<DbSet<TRegistro>, IQueryable<TRegistro>> consulta,
        Func<TRegistro, T> leitura,
        CancellationToken cancelamento)
    {
        var registros = await consulta(Registros).AsNoTracking().ToListAsync(cancelamento);

        return [.. registros.Select(registro => new Versionado<T>(Abrir(registro, leitura), registro.Versao))];
    }

    /// <param name="restricoes">O que dizer quando uma restrição do banco recusa a gravação, pelo nome da restrição.</param>
    /// <param name="eventos">Os fatos do agregado desde a última gravação, para a caixa de saída (ADR-0016).</param>
    public async Task<int> Inserir(
        TRegistro registro,
        IReadOnlyDictionary<string, string> restricoes,
        CancellationToken cancelamento,
        IReadOnlyList<EventoDeDominio>? eventos = null)
    {
        registro.ConstrutoraId = contexto.ConstrutoraQueGrava;
        var mensagens = Mensagens(eventos, registro.Versao);

        await using var transacao = await TransacaoParaAsMensagens(mensagens, cancelamento);

        Registros.Add(registro);

        try
        {
            await contexto.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException erro) when (Violacao(erro) is { SqlState: PostgresErrorCodes.UniqueViolation } violacao)
        {
            throw new AgregadoJaExiste(
                violacao.ConstraintName?.StartsWith("pk_", StringComparison.Ordinal) == true
                    ? $"Já existe {agregado} {registro.Id}."
                    : Mensagem(restricoes, violacao),
                erro);
        }
        catch (DbUpdateException erro) when (Violacao(erro) is { SqlState: PostgresErrorCodes.ForeignKeyViolation } violacao)
        {
            throw new AgregadoNaoEncontrado(Mensagem(restricoes, violacao), erro);
        }
        finally
        {
            contexto.ChangeTracker.Clear();
        }

        await GravarNaCaixa(mensagens, cancelamento);

        if (transacao is not null)
        {
            await transacao.CommitAsync(cancelamento);
        }

        return registro.Versao;
    }

    /// <param name="outrasColunas">As colunas próprias da tabela, além das que toda tabela de agregado tem.</param>
    /// <param name="restricoes">O que dizer quando uma restrição do banco recusa a gravação, pelo nome da restrição.</param>
    /// <param name="eventos">Os fatos do agregado desde a última gravação, para a caixa de saída (ADR-0016).</param>
    public async Task<int> Atualizar(
        TRegistro novo,
        int versaoLida,
        Action<UpdateSettersBuilder<TRegistro>>? outrasColunas,
        IReadOnlyDictionary<string, string> restricoes,
        CancellationToken cancelamento,
        IReadOnlyList<EventoDeDominio>? eventos = null)
    {
        var mensagens = Mensagens(eventos, novo.Versao);

        await using var transacao = await TransacaoParaAsMensagens(mensagens, cancelamento);

        int alteradas;

        try
        {
            alteradas = await Registros
                .Where(registro => registro.Id == novo.Id && registro.Versao == versaoLida)
                .ExecuteUpdateAsync(
                    colunas =>
                    {
                        colunas
                            .SetProperty(registro => registro.Versao, novo.Versao)
                            .SetProperty(registro => registro.Formato, novo.Formato)
                            .SetProperty(registro => registro.Documento, novo.Documento)
                            .SetProperty(registro => registro.AtualizadoEm, novo.AtualizadoEm);

                        outrasColunas?.Invoke(colunas);
                    },
                    cancelamento);
        }
        catch (PostgresException erro) when (erro.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new AgregadoJaExiste(Mensagem(restricoes, erro), erro);
        }

        if (alteradas == 0)
        {
            await AcusarConflitoOuAusencia(novo.Id, versaoLida, cancelamento);
        }

        await GravarNaCaixa(mensagens, cancelamento);

        if (transacao is not null)
        {
            await transacao.CommitAsync(cancelamento);
        }

        return novo.Versao;
    }

    /// <param name="emUso">O que dizer quando outro agregado aponta para este, pelo nome da chave estrangeira.</param>
    public async Task Apagar(Guid id, int versaoLida, IReadOnlyDictionary<string, string> emUso, CancellationToken cancelamento)
    {
        int apagadas;

        try
        {
            apagadas = await Registros
                .Where(registro => registro.Id == id && registro.Versao == versaoLida)
                .ExecuteDeleteAsync(cancelamento);
        }
        catch (PostgresException erro) when (erro.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new AgregadoEmUso(Mensagem(emUso, erro), erro);
        }

        if (apagadas == 0)
        {
            await AcusarConflitoOuAusencia(id, versaoLida, cancelamento);
        }
    }

    private List<RegistroDeMensagem> Mensagens(IReadOnlyList<EventoDeDominio>? eventos, int versaoDoAgregado) =>
        [.. (eventos ?? []).Select(evento => RegistroDeMensagem.De(evento, contexto.ConstrutoraQueGrava, versaoDoAgregado, Agora))];

    /// <summary>
    /// O agregado e as mensagens dele numa transação só: ou ficam os dois, ou
    /// nenhum — num conflito de versão, nem um nem outro. Sem mensagem, ou
    /// dentro da transação de quem chamou, não precisa abrir outra.
    /// </summary>
    private async Task<IDbContextTransaction?> TransacaoParaAsMensagens(List<RegistroDeMensagem> mensagens, CancellationToken cancelamento) =>
        mensagens.Count > 0 && contexto.Database.CurrentTransaction is null
            ? await contexto.Database.BeginTransactionAsync(cancelamento)
            : null;

    /// <summary>
    /// As mensagens entram uma a uma, na ordem dos fatos: a sequência da caixa
    /// é a ordem em que eles aconteceram. Todas num SaveChanges só, não — o
    /// Entity Framework ordena as inserções pela chave, e a chave de duas
    /// mensagens do mesmo milissegundo sai em ordem qualquer.
    /// </summary>
    /// <remarks>
    /// Cada publicação é um passo no rastro de quem gravou — o pedido HTTP, na
    /// API —, e a mensagem leva o contexto dela: o tratamento, em outro
    /// processo e talvez minutos depois, continua o mesmo rastro.
    /// </remarks>
    private async Task GravarNaCaixa(List<RegistroDeMensagem> mensagens, CancellationToken cancelamento)
    {
        try
        {
            foreach (var mensagem in mensagens)
            {
                using var publicacao = TelemetriaDaCaixaDeSaida.Fonte.StartActivity(
                    $"publicar {mensagem.Tipo}",
                    ActivityKind.Producer,
                    parentContext: default,
                    tags: TelemetriaDaCaixaDeSaida.Atributos(mensagem.Id, mensagem.Tipo, mensagem.ConstrutoraId, "send"));

                mensagem.ContextoDoRastreio = (publicacao ?? Activity.Current)?.Id;
                contexto.CaixaDeSaida.Add(mensagem);
                await contexto.SaveChangesAsync(cancelamento);
            }
        }
        finally
        {
            contexto.ChangeTracker.Clear();
        }
    }

    /// <summary>Refaz o agregado; documento que não se lê vira <see cref="DocumentoInvalido"/>, com o id.</summary>
    private T Abrir<T>(TRegistro registro, Func<TRegistro, T> leitura)
    {
        try
        {
            return leitura(registro);
        }
        catch (Exception erro) when (erro is JsonException or ExcecaoDeDominio or InvalidOperationException)
        {
            throw new DocumentoInvalido($"{Maiuscula(agregado)} {registro.Id} não se lê: {erro.Message}", erro);
        }
    }

    private async Task AcusarConflitoOuAusencia(Guid id, int versaoLida, CancellationToken cancelamento)
    {
        var atual = await Registros
            .Where(registro => registro.Id == id)
            .Select(registro => (int?)registro.Versao)
            .SingleOrDefaultAsync(cancelamento);

        throw atual is null
            ? new AgregadoNaoEncontrado($"Não existe {agregado} {id}.")
            : new ConflitoDeConcorrencia(
                $"{Maiuscula(agregado)} {id} mudou desde a versão {versaoLida}: está na versão {atual}. Leia de novo e refaça a alteração.",
                atual.Value);
    }

    /// <summary>A mensagem para a restrição que recusou; restrição que ninguém previu é defeito, e não erro de quem usa.</summary>
    private static string Mensagem(IReadOnlyDictionary<string, string> restricoes, PostgresException erro) =>
        erro.ConstraintName is { } restricao && restricoes.TryGetValue(restricao, out var mensagem)
            ? mensagem
            : throw new InvalidOperationException($"O banco recusou a gravação pela restrição {erro.ConstraintName}, que ninguém previu.", erro);

    /// <summary>O erro do PostgreSQL por trás de uma exceção do Entity Framework, se houver.</summary>
    private static PostgresException? Violacao(DbUpdateException erro) => erro.InnerException as PostgresException;

    private static string Maiuscula(string texto) => string.Concat(char.ToUpperInvariant(texto[0]).ToString(), texto[1..]);
}

/// <summary>A listagem das tabelas que têm nome: em ordem de nome, sem abrir documento.</summary>
internal static class ListagemPorNome
{
    public static async Task<IReadOnlyList<ResumoDoAgregado<TId>>> Listar<TRegistro, TId>(
        this DbSet<TRegistro> registros,
        Func<Guid, TId> id,
        CancellationToken cancelamento)
        where TRegistro : RegistroComNome
        where TId : Identificador
    {
        var linhas = await registros
            .AsNoTracking()
            .OrderBy(registro => registro.Nome)
            .ThenBy(registro => registro.Id)
            .Select(registro => new { registro.Id, registro.Nome, registro.Versao, registro.AtualizadoEm })
            .ToListAsync(cancelamento);

        return [.. linhas.Select(linha => new ResumoDoAgregado<TId>(id(linha.Id), linha.Nome, linha.Versao, linha.AtualizadoEm))];
    }
}
