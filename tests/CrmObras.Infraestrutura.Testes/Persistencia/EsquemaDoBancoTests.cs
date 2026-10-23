using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/*
 * As migrações aplicadas num PostgreSQL de verdade, e o banco cumprindo o que
 * prometem: as tabelas, as restrições, as chaves estrangeiras e a segurança
 * por linha. As inserções aqui são SQL direto, com o dono do banco, de
 * propósito — é o banco que está sendo testado, não o código que normalmente
 * escreve nele.
 */
public sealed class EsquemaDoBancoTests(BancoDeTestes banco)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly Guid _construtora = Guid.CreateVersion7();

    [Fact]
    public async Task As_migracoes_criam_uma_tabela_por_agregado_e_a_das_chaves_de_idempotencia()
    {
        var tabelas = await Consultar(
            "select table_name from information_schema.tables where table_schema = 'crm' order by table_name",
            leitor => leitor.GetString(0));

        Assert.Equal(["atividades", "chaves_de_idempotencia", "contas", "funis", "historico_de_migracoes", "leads", "oportunidades", "propostas"], tabelas);
    }

    [Fact]
    public async Task Nao_ha_migracao_pendente_no_banco()
    {
        await using var contexto = banco.NovoContextoSemConstrutora();

        Assert.Empty(await contexto.Database.GetPendingMigrationsAsync(Cancelamento));
    }

    [Fact]
    public async Task Toda_tabela_com_dados_de_construtora_tem_seguranca_por_linha_forcada()
    {
        /*
         * Tabela nova que esquecer a política fica aberta para todas as
         * construtoras, sem erro nenhum. Este teste é o que lembra.
         */
        var tabelas = await Consultar(
            """
            select c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   exists (select 1 from pg_policies p where p.schemaname = 'crm' and p.tablename = c.relname)
            from pg_class c join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'crm' and c.relkind = 'r' and c.relname <> 'historico_de_migracoes'
            order by c.relname
            """,
            leitor => (Tabela: leitor.GetString(0), Ativada: leitor.GetBoolean(1), Forcada: leitor.GetBoolean(2), TemPolitica: leitor.GetBoolean(3)));

        Assert.Equal(7, tabelas.Count);
        Assert.All(tabelas, tabela => Assert.True(tabela.Ativada && tabela.Forcada && tabela.TemPolitica, $"{tabela.Tabela} sem segurança por linha."));
    }

    [Theory]
    [InlineData("versao", "0", "ck_funis_versao")]
    [InlineData("formato", "0", "ck_funis_formato")]
    [InlineData("documento", "'[]'", "ck_funis_documento")]
    public async Task O_banco_recusa_versao_formato_e_documento_que_nao_servem(string coluna, string valor, string restricao)
    {
        var colunas = new Dictionary<string, string> { ["versao"] = "1", ["formato"] = "1", ["documento"] = "'{}'" };
        colunas[coluna] = valor;

        var erro = await Recusado(
            $"""
            insert into crm.funis (id, construtora_id, nome, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, 'Vendas', {colunas["versao"]}, {colunas["formato"]}, {colunas["documento"]}::jsonb, now(), now())
            """,
            Guid.CreateVersion7(),
            _construtora);

        Assert.Equal((PostgresErrorCodes.CheckViolation, restricao), (erro.SqlState, erro.ConstraintName));
    }

    [Theory]
    [InlineData("leads", "Novo", "Qualificado", "Convertido")]
    [InlineData("oportunidades", "Aberta", "Ganha", "Perdida")]
    [InlineData("atividades", "Pendente", "Concluida", "Cancelada")]
    public async Task A_situacao_so_tem_os_valores_que_o_dominio_tem(string tabela, params string[] algumas)
    {
        var permitidas = await Consultar(
            "select pg_get_constraintdef(oid) from pg_constraint where conname = @p0",
            leitor => leitor.GetString(0),
            $"ck_{tabela}_situacao");

        var definicao = Assert.Single(permitidas);
        Assert.All(algumas, situacao => Assert.Contains($"'{situacao}'", definicao, StringComparison.Ordinal));

        var erro = await Recusado(
            "insert into crm.leads (id, construtora_id, nome, situacao, versao, formato, documento, criado_em, atualizado_em) values (@p0, @p1, 'Marina', 'Esquecido', 1, 1, '{}', now(), now())",
            Guid.CreateVersion7(),
            _construtora);
        Assert.Equal("ck_leads_situacao", erro.ConstraintName);
    }

    [Fact]
    public async Task O_cnpj_ou_cpf_nao_se_repete_na_construtora()
    {
        var outra = Guid.CreateVersion7();
        await InserirConta(_construtora, "11222333000181");
        await InserirConta(outra, "11222333000181");
        await InserirConta(_construtora, cnpjOuCpf: null);
        await InserirConta(_construtora, cnpjOuCpf: null);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirConta(_construtora, "11222333000181"));

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "ux_contas_construtora_id_cnpj_ou_cpf"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task A_venda_aponta_para_conta_e_funil_que_existem()
    {
        var conta = await InserirConta(_construtora);

        var semFunil = await Assert.ThrowsAsync<PostgresException>(() => InserirOportunidade(_construtora, conta, Guid.CreateVersion7()));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_oportunidades_funis"), (semFunil.SqlState, semFunil.ConstraintName));
    }

    [Fact]
    public async Task A_referencia_nao_atravessa_construtoras()
    {
        /*
         * Chave estrangeira não passa pela segurança por linha: é a chave
         * composta, com a construtora, que não deixa uma venda apontar para a
         * conta de outra construtora.
         */
        var outra = Guid.CreateVersion7();
        var contaDaOutra = await InserirConta(outra);
        var funil = await InserirFunil(_construtora);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirOportunidade(_construtora, contaDaOutra, funil));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_oportunidades_contas"), (erro.SqlState, erro.ConstraintName));
    }

    [Theory]
    [InlineData("contas", "fk_oportunidades_contas")]
    [InlineData("funis", "fk_oportunidades_funis")]
    public async Task O_referenciado_nao_sai_do_banco_enquanto_alguem_aponta_para_ele(string tabela, string restricao)
    {
        var conta = await InserirConta(_construtora);
        var funil = await InserirFunil(_construtora);
        await InserirOportunidade(_construtora, conta, funil);

        var erro = await Recusado($"delete from crm.{tabela} where id = @p0", tabela == "contas" ? conta : funil);

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, restricao), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task A_atividade_e_a_proposta_apontam_para_venda_da_mesma_construtora()
    {
        var conta = await InserirConta(_construtora);
        var venda = await InserirOportunidade(_construtora, conta, await InserirFunil(_construtora));
        var vendaDaOutra = Guid.CreateVersion7();

        var atividade = await Recusado(
            """
            insert into crm.atividades (id, construtora_id, conta_id, oportunidade_id, responsavel, situacao, prevista_para, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, @p2, @p3, 'carlos.souza', 'Pendente', now(), 1, 1, '{}', now(), now())
            """,
            Guid.CreateVersion7(),
            _construtora,
            conta,
            vendaDaOutra);
        await Executar(
            """
            insert into crm.atividades (id, construtora_id, conta_id, oportunidade_id, responsavel, situacao, prevista_para, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, @p2, null, 'carlos.souza', 'Pendente', now(), 1, 1, '{}', now(), now())
            """,
            Guid.CreateVersion7(),
            _construtora,
            conta);
        var proposta = await Recusado(
            "insert into crm.propostas (id, construtora_id, oportunidade_id, conta_id, versao, formato, documento, criado_em, atualizado_em) values (@p0, @p1, @p2, @p3, 1, 1, '{}', now(), now())",
            Guid.CreateVersion7(),
            _construtora,
            vendaDaOutra,
            conta);

        Assert.Equal("fk_atividades_oportunidades", atividade.ConstraintName);
        Assert.Equal("fk_propostas_oportunidades", proposta.ConstraintName);
        Assert.NotEqual(Guid.Empty, venda);
    }

    private async Task<Guid> InserirConta(Guid construtora, string? cnpjOuCpf = null)
    {
        var id = Guid.CreateVersion7();
        await Executar(
            "insert into crm.contas (id, construtora_id, nome, cnpj_ou_cpf, versao, formato, documento, criado_em, atualizado_em) values (@p0, @p1, 'Rota Sul', @p2, 1, 1, '{}', now(), now())",
            id,
            construtora,
            (object?)cnpjOuCpf ?? DBNull.Value);

        return id;
    }

    private async Task<Guid> InserirFunil(Guid construtora)
    {
        var id = Guid.CreateVersion7();
        await Executar(
            "insert into crm.funis (id, construtora_id, nome, versao, formato, documento, criado_em, atualizado_em) values (@p0, @p1, 'Vendas', 1, 1, '{}', now(), now())",
            id,
            construtora);

        return id;
    }

    private async Task<Guid> InserirOportunidade(Guid construtora, Guid conta, Guid funil)
    {
        var id = Guid.CreateVersion7();
        await Executar(
            """
            insert into crm.oportunidades (id, construtora_id, conta_id, funil_id, titulo, responsavel, situacao, versao, formato, documento, criado_em, atualizado_em)
            values (@p0, @p1, @p2, @p3, 'Galpão', 'carlos.souza', 'Aberta', 1, 1, '{}', now(), now())
            """,
            id,
            construtora,
            conta,
            funil);

        return id;
    }

    private async Task<int> Executar(string sql, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

        return await comando.ExecuteNonQueryAsync(Cancelamento);
    }

    private async Task<List<T>> Consultar<T>(string sql, Func<NpgsqlDataReader, T> linha, params object[] parametros)
    {
        await using var conexao = new NpgsqlConnection(banco.Conexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(sql, conexao);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.AddWithValue($"p{i}", parametros[i]);
        }

        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);

        var linhas = new List<T>();

        while (await leitor.ReadAsync(Cancelamento))
        {
            linhas.Add(linha(leitor));
        }

        return linhas;
    }

    private async Task<PostgresException> Recusado(string sql, params object[] parametros) =>
        await Assert.ThrowsAsync<PostgresException>(() => Executar(sql, parametros));
}
