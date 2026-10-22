using CrmObras.Infraestrutura.Persistencia;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

/// <summary>
/// Um PostgreSQL de verdade, num contêiner, para o assembly de testes
/// inteiro: sobe uma vez, recebe as migrações, e cada teste trabalha com
/// dados próprios — construtora e ids novos —, sem precisar limpar o banco.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL de verdade, e não SQLite nem o provedor em memória: o que está
/// sendo testado é justamente o que só o PostgreSQL faz — <c>jsonb</c>,
/// restrições, chaves estrangeiras compostas, segurança por linha, os códigos
/// de erro que ele devolve (ADR-0011).
/// </para>
/// <para>
/// Duas conexões. A do dono do banco, superusuário, aplica as migrações e
/// serve aos testes que examinam o banco por dentro. A da aplicação usa um
/// papel comum, como em produção: é ela que a segurança por linha alcança.
/// </para>
/// <para>
/// Sem Docker na máquina de quem desenvolve, os testes que precisam do banco
/// são pulados, com o motivo. No CI não: lá, Docker indisponível é falha —
/// teste de integração que some sem ninguém ver é pior do que teste que
/// falha.
/// </para>
/// </remarks>
public sealed class BancoDeTestes : IAsyncLifetime
{
    /// <summary>Versão fixa: o banco dos testes não muda sozinho de uma execução para outra.</summary>
    public const string Imagem = "postgres:17.6-alpine";

    /// <summary>O papel da aplicação: entra, lê e grava, mas não é superusuário nem passa por cima da segurança por linha.</summary>
    public const string PapelDaAplicacao = "crm_aplicacao";

    private const string SenhaDaAplicacao = "aplicacao-nos-testes";

    private PostgreSqlContainer? _conteiner;

    private string? _conexao;

    private string? _conexaoDaAplicacao;

    private string? _indisponivel;

    private static bool EmIntegracaoContinua =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _conteiner = new PostgreSqlBuilder(Imagem).Build();
            await _conteiner.StartAsync();
        }
        catch (DockerUnavailableException erro) when (!EmIntegracaoContinua)
        {
            _indisponivel = $"Docker indisponível nesta máquina; os testes com PostgreSQL foram pulados. ({erro.Message})";

            return;
        }

        _conexao = _conteiner.GetConnectionString();

        await using (var contexto = ContextoDoBanco.Criar(_conexao, ConstrutoraFixa.Nenhuma))
        {
            await contexto.Database.MigrateAsync();
        }

        await CriarPapelDaAplicacao(_conexao);

        _conexaoDaAplicacao = new NpgsqlConnectionStringBuilder(_conexao) { Username = PapelDaAplicacao, Password = SenhaDaAplicacao }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_conteiner is not null)
        {
            await _conteiner.DisposeAsync();
        }
    }

    /// <summary>A conexão do dono do banco — superusuário, que passa por cima da segurança por linha. Pula o teste se não houver banco.</summary>
    public string Conexao
    {
        get
        {
            Assert.SkipWhen(_conexao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexao!;
        }
    }

    /// <summary>A conexão da aplicação, com o papel comum que a segurança por linha alcança.</summary>
    public string ConexaoDaAplicacao
    {
        get
        {
            Assert.SkipWhen(_conexaoDaAplicacao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _conexaoDaAplicacao!;
        }
    }

    /// <summary>Um contexto novo em nome da construtora, como cada requisição da aplicação terá o seu.</summary>
    public ContextoDoBanco NovoContexto(Guid construtora) => ContextoDoBanco.Criar(ConexaoDaAplicacao, new ConstrutoraFixa(construtora));

    /// <summary>Um contexto em nome de nenhuma construtora: não lê nem grava agregado.</summary>
    public ContextoDoBanco NovoContextoSemConstrutora() => ContextoDoBanco.Criar(ConexaoDaAplicacao, ConstrutoraFixa.Nenhuma);

    /// <summary>O papel como quem implanta o criaria: lê e grava as tabelas do esquema, e nada mais.</summary>
    private static async Task CriarPapelDaAplicacao(string conexaoDoDono)
    {
        await using var conexao = new NpgsqlConnection(conexaoDoDono);
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            create role {PapelDaAplicacao} login password '{SenhaDaAplicacao}';
            grant usage on schema crm to {PapelDaAplicacao};
            grant select, insert, update, delete on all tables in schema crm to {PapelDaAplicacao};
            """,
            conexao);
        await comando.ExecuteNonQueryAsync();
    }
}
