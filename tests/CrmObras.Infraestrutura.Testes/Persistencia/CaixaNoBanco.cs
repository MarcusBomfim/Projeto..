using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/// <summary>Uma mensagem da caixa de saída como está no banco.</summary>
public sealed record MensagemNoBanco(
    Guid Id,
    Guid Construtora,
    string Tipo,
    short Formato,
    string Conteudo,
    int VersaoDoAgregado,
    int Tentativas,
    DateTimeOffset ProximaTentativaEm,
    DateTimeOffset? TratadaEm,
    DateTimeOffset? AbandonadaEm,
    string? UltimoErro,
    string? ContextoDoRastreio);

/// <summary>
/// A caixa de saída vista pelo dono do banco, por cima da segurança por
/// linha: o que o teste confere, e o que ele prepara sem passar pelo domínio.
/// </summary>
public static class CaixaNoBanco
{
    private const string Colunas =
        "id, construtora_id, tipo, formato, conteudo::text, versao_do_agregado, tentativas, proxima_tentativa_em, tratada_em, abandonada_em, ultimo_erro, contexto_do_rastreio";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    /// <summary>Tira tudo da caixa: o que o teste reservar será o que ele mesmo gravou.</summary>
    public static Task Esvaziar(BancoDeTestes banco) => Executar(banco, "delete from crm.caixa_de_saida");

    public static Task<List<MensagemNoBanco>> DoAgregado(BancoDeTestes banco, Guid agregado) =>
        Consultar(banco, $"select {Colunas} from crm.caixa_de_saida where agregado_id = @p0 order by sequencia", agregado);

    public static Task<List<MensagemNoBanco>> Todas(BancoDeTestes banco) =>
        Consultar(banco, $"select {Colunas} from crm.caixa_de_saida order by sequencia");

    /// <summary>Uma mensagem escrita direto no banco — de um tipo qualquer, no estado que o teste quiser.</summary>
    /// <param name="conteudo">O JSON da mensagem; por padrão, um objeto vazio.</param>
    public static async Task<Guid> GravarCrua(
        BancoDeTestes banco,
        Guid construtora,
        string tipo,
        DateTimeOffset ocorridaEm,
        DateTimeOffset? tratadaEm = null,
        DateTimeOffset? abandonadaEm = null,
        string conteudo = "{}")
    {
        var id = Guid.CreateVersion7();

        await Executar(
            banco,
            """
            insert into crm.caixa_de_saida (
                id, construtora_id, tipo, formato, conteudo, agregado_id, versao_do_agregado,
                ocorrida_em, tentativas, proxima_tentativa_em, tratada_em, abandonada_em)
            values (@p0, @p1, @p2, 1, @p3::jsonb, @p4, 1, @p5, 0, @p5, @p6, @p7)
            """,
            id,
            construtora,
            tipo,
            conteudo,
            Guid.CreateVersion7(),
            ocorridaEm.ToUniversalTime(),
            (object?)tratadaEm?.ToUniversalTime() ?? DBNull.Value,
            (object?)abandonadaEm?.ToUniversalTime() ?? DBNull.Value);

        return id;
    }

    public static async Task<int> Executar(BancoDeTestes banco, string sql, params object[] parametros)
    {
        ArgumentNullException.ThrowIfNull(banco);

        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = Comando(conexao, sql, parametros);

        return await comando.ExecuteNonQueryAsync(Cancelamento);
    }

    private static async Task<List<MensagemNoBanco>> Consultar(BancoDeTestes banco, string sql, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = Comando(conexao, sql, parametros);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var mensagens = new List<MensagemNoBanco>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            mensagens.Add(new MensagemNoBanco(
                leitor.GetGuid(0),
                leitor.GetGuid(1),
                leitor.GetString(2),
                leitor.GetInt16(3),
                leitor.GetString(4),
                leitor.GetInt32(5),
                leitor.GetInt32(6),
                leitor.GetFieldValue<DateTimeOffset>(7),
                leitor.IsDBNull(8) ? null : leitor.GetFieldValue<DateTimeOffset>(8),
                leitor.IsDBNull(9) ? null : leitor.GetFieldValue<DateTimeOffset>(9),
                leitor.IsDBNull(10) ? null : leitor.GetString(10),
                leitor.IsDBNull(11) ? null : leitor.GetString(11)));
        }

        return mensagens;
    }

    private static NpgsqlCommand Comando(NpgsqlConnection conexao, string sql, object[] parametros)
    {
        var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

        return comando;
    }
}
