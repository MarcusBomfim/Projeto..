using System.Globalization;
using CrmObras.Api.Http;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;
using CrmObras.Dominio.MeiosDeContato;
using CrmObras.Infraestrutura.Persistencia;

namespace CrmObras.Api.Recursos;

/// <summary>O que se lista de um agregado: o id, o nome, a versão e quando foi gravado pela última vez.</summary>
public sealed record ResumoResposta(Guid Id, string Nome, int Versao, DateTimeOffset AtualizadoEm)
{
    public static ResumoResposta De<TId>(ResumoDoAgregado<TId> resumo)
        where TId : Identificador
    {
        ArgumentNullException.ThrowIfNull(resumo);

        return new(resumo.Id.Valor, resumo.Nome, resumo.Versao, resumo.AtualizadoEm);
    }
}

/// <summary>Um nome novo.</summary>
public sealed record NovoNome(string Nome);

/// <summary>O que os endpoints fazem com um agregado lido antes de alterá-lo.</summary>
internal static class Lidos
{
    /// <summary>
    /// A alteração parte da versão que o cliente leu. Se o banco já está em
    /// outra, ela nem é aplicada — 412, com a versão atual. O banco confere de
    /// novo ao gravar, para quem gravar no meio do caminho (ADR-0012).
    /// </summary>
    /// <param name="agregado">Como o agregado aparece na mensagem: "A conta 0199…".</param>
    public static void ConferirVersao<T>(Versionado<T> lido, VersaoLida versao, string agregado)
    {
        ArgumentNullException.ThrowIfNull(lido);
        ArgumentNullException.ThrowIfNull(versao);

        if (lido.Versao != versao.Valor)
        {
            throw new ConflitoDeConcorrencia(
                $"{agregado} mudou desde a versão {versao.Valor}: está na versão {lido.Versao}. Leia de novo e refaça a alteração.",
                lido.Versao);
        }
    }
}

/// <summary>
/// Os valores do domínio que chegam como texto no pedido. O que não veio é
/// nulo; o que veio é lido pelas regras do domínio, e o que elas recusam é
/// 422, com a explicação delas.
/// </summary>
internal static class TextosDoPedido
{
    public static Documento? ComoDocumento(this string? texto) => texto is null ? null : Documento.De(texto);

    public static Email? ComoEmail(this string? texto) => texto is null ? null : Email.De(texto);

    public static Telefone? ComoTelefone(this string? texto) => texto is null ? null : Telefone.De(texto);

    /// <summary>
    /// Uma opção que vem na consulta — <c>?situacao=Novo</c> —, só pelo nome,
    /// como no corpo: número não é opção, e nome errado é 400, com as que
    /// existem.
    /// </summary>
    /// <param name="parametro">O nome do parâmetro, para a mensagem.</param>
    public static TOpcao ComoOpcao<TOpcao>(this string texto, string parametro)
        where TOpcao : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Enum.GetNames<TOpcao>().Contains(texto, StringComparer.Ordinal)
            ? Enum.Parse<TOpcao>(texto)
            : throw new PedidoInvalido($"O parâmetro {parametro} é um de: {string.Join(", ", Enum.GetNames<TOpcao>())}; veio \"{texto}\".");
    }

    /// <summary>Um dia na consulta, como <c>2026-10-19</c> — e só assim: <c>10/19/2026</c> e <c>19/10/2026</c> são ambíguos.</summary>
    public static DateOnly ComoDia(this string texto, string parametro)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? dia
            : throw new PedidoInvalido($"O parâmetro {parametro} é um dia, como 2026-10-19; veio \"{texto}\".");
    }

    /// <summary>Um mês na consulta, como <c>2026-10</c>.</summary>
    public static Mes ComoMes(this string texto, string parametro)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return DateOnly.TryParseExact(texto, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? Mes.De(dia)
            : throw new PedidoInvalido($"O parâmetro {parametro} é um mês, como 2026-10; veio \"{texto}\".");
    }

    /// <summary>Um fuso na consulta, pelo nome da base IANA: <c>Europe/Lisbon</c>.</summary>
    public static TimeZoneInfo ComoFuso(this string texto, string parametro)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return Fuso.Ler(texto)
            ?? throw new PedidoInvalido($"O parâmetro {parametro} é um fuso da base IANA, como {Fuso.Padrao}; veio \"{texto}\".");
    }
}

/// <summary>
/// As operações que gravam mais de um agregado — o lead que vira conta, a
/// proposta aceita que ganha a venda: ou fica tudo, ou nada.
/// </summary>
internal static class Transacoes
{
    /// <summary>
    /// Executa a operação numa transação. Se já há uma — a da chave de
    /// idempotência (ADR-0013) —, a operação entra nela, e quem a abriu
    /// confirma.
    /// </summary>
    public static async Task<T> NumaTransacao<T>(this ContextoDoBanco contexto, Func<Task<T>> operacao, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(operacao);

        if (contexto.Database.CurrentTransaction is not null)
        {
            return await operacao();
        }

        await using var transacao = await contexto.Database.BeginTransactionAsync(cancelamento);
        var resultado = await operacao();
        await transacao.CommitAsync(cancelamento);

        return resultado;
    }
}
