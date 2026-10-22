using System.Text.Json;
using CrmObras.Dominio.Comum;
using CrmObras.Infraestrutura.Persistencia.Registros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
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

    /// <summary>Os agregados que a consulta escolhe, cada um com a versão em que estava.</summary>
    public async Task<IReadOnlyList<Versionado<T>>> Consultar<T>(
        Func<IQueryable<TRegistro>, IQueryable<TRegistro>> consulta,
        Func<TRegistro, T> leitura,
        CancellationToken cancelamento)
    {
        var registros = await consulta(Registros.AsNoTracking()).ToListAsync(cancelamento);

        return [.. registros.Select(registro => new Versionado<T>(Abrir(registro, leitura), registro.Versao))];
    }

    /// <param name="restricoes">O que dizer quando uma restrição do banco recusa a gravação, pelo nome da restrição.</param>
    public async Task<int> Inserir(TRegistro registro, IReadOnlyDictionary<string, string> restricoes, CancellationToken cancelamento)
    {
        registro.ConstrutoraId = contexto.ConstrutoraQueGrava;
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

        return registro.Versao;
    }

    /// <param name="outrasColunas">As colunas próprias da tabela, além das que toda tabela de agregado tem.</param>
    /// <param name="restricoes">O que dizer quando uma restrição do banco recusa a gravação, pelo nome da restrição.</param>
    public async Task<int> Atualizar(
        TRegistro novo,
        int versaoLida,
        Action<UpdateSettersBuilder<TRegistro>>? outrasColunas,
        IReadOnlyDictionary<string, string> restricoes,
        CancellationToken cancelamento)
    {
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
