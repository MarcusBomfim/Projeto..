using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CrmObras.Infraestrutura.Persistencia.Registros;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// O banco: uma tabela por tipo de agregado, cada linha um documento
/// <c>jsonb</c> com identidade, versão e o que se consulta sem abri-lo — e a
/// construtora dona dele (ADR-0011).
/// </summary>
/// <remarks>
/// <para>
/// O que o banco consegue garantir sem duplicar regra de negócio, ele
/// garante: identidade única, versão e formato válidos, documento que é um
/// objeto JSON, situação que existe, o CNPJ ou o CPF que não se repete na
/// construtora, e as referências entre agregados — a venda de uma conta e de
/// um funil da mesma construtora, que não somem enquanto alguém aponta para
/// eles. As regras do negócio ficam no domínio.
/// </para>
/// <para>
/// Toda consulta sai filtrada pela construtora do contexto, e o PostgreSQL
/// confere de novo, com segurança por linha: uma consulta que escape do
/// filtro — SQL escrito à mão, um <c>IgnoreQueryFilters</c> — ainda não vê o
/// que é de outra construtora.
/// </para>
/// <para>
/// Nomes em snake_case, no esquema <c>crm</c>, como se escreve SQL à mão —
/// quem consulta o banco direto não precisa de aspas.
/// </para>
/// </remarks>
public sealed class ContextoDoBanco(DbContextOptions<ContextoDoBanco> opcoes, IConstrutoraAtual construtora) : DbContext(opcoes)
{
    public const string Esquema = "crm";

    /// <summary>O nome do filtro global pela construtora, em toda tabela.</summary>
    internal const string FiltroDaConstrutora = "construtora";

    internal DbSet<RegistroDeConta> Contas => Set<RegistroDeConta>();

    internal DbSet<RegistroDeLead> Leads => Set<RegistroDeLead>();

    internal DbSet<RegistroDeFunil> Funis => Set<RegistroDeFunil>();

    internal DbSet<RegistroDeOportunidade> Oportunidades => Set<RegistroDeOportunidade>();

    internal DbSet<RegistroDeAtividade> Atividades => Set<RegistroDeAtividade>();

    internal DbSet<RegistroDeProposta> Propostas => Set<RegistroDeProposta>();

    /// <summary>A construtora em nome de quem este contexto lê e grava; <see langword="null"/> se nenhuma.</summary>
    internal Guid? ConstrutoraId => construtora.Id;

    /// <summary>A construtora de quem grava. Sem construtora, nada se grava.</summary>
    internal Guid ConstrutoraQueGrava =>
        ConstrutoraId ?? throw new InvalidOperationException(
            "Sem construtora, nada se grava: quem hospeda a persistência precisa dizer em nome de qual construtora ela trabalha.");

    /// <summary>
    /// O Npgsql configurado como as migrações esperam — inclusive o histórico
    /// delas dentro do esquema do projeto —, e a construtora passada ao banco
    /// a cada conexão aberta. Usado pela aplicação, pelo dotnet-ef e pelos
    /// testes, para os três verem o mesmo banco.
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opcoes, string conexao)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        opcoes
            .UseNpgsql(conexao, npgsql => npgsql.MigrationsHistoryTable("historico_de_migracoes", Esquema))
            .AddInterceptors(ConstrutoraNaConexao.Instancia);
    }

    /// <summary>Um contexto avulso para a cadeia de conexão — para ferramentas e testes, fora da injeção de dependência.</summary>
    public static ContextoDoBanco Criar(string conexao, IConstrutoraAtual construtora)
    {
        var opcoes = new DbContextOptionsBuilder<ContextoDoBanco>();
        Configurar(opcoes, conexao);

        return new ContextoDoBanco(opcoes.Options, construtora);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        var contas = Agregado<RegistroDeConta>(modelBuilder, "contas");
        ComNome(contas, "contas", 200);
        contas.Property(registro => registro.CnpjOuCpf).HasColumnName("cnpj_ou_cpf").HasMaxLength(14);
        contas
            .HasIndex(registro => new { registro.ConstrutoraId, registro.CnpjOuCpf })
            .IsUnique()
            .HasFilter("cnpj_ou_cpf is not null")
            .HasDatabaseName("ux_contas_construtora_id_cnpj_ou_cpf");
        Referenciavel(contas, "contas");

        var leads = Agregado<RegistroDeLead>(modelBuilder, "leads");
        ComNome(leads, "leads", 200);
        ComSituacao(leads, "leads", registro => registro.Situacao);

        var funis = Agregado<RegistroDeFunil>(modelBuilder, "funis");
        ComNome(funis, "funis", 100);
        Referenciavel(funis, "funis");

        var oportunidades = Agregado<RegistroDeOportunidade>(modelBuilder, "oportunidades");
        oportunidades.Property(registro => registro.Titulo).HasColumnName("titulo").HasMaxLength(200);
        oportunidades.Property(registro => registro.Responsavel).HasColumnName("responsavel").HasMaxLength(100);
        ComSituacao(oportunidades, "oportunidades", registro => registro.Situacao);
        Referencia<RegistroDeOportunidade, RegistroDeConta>(oportunidades, "oportunidades", "contas", nameof(RegistroDeOportunidade.ContaId), "conta_id");
        Referencia<RegistroDeOportunidade, RegistroDeFunil>(oportunidades, "oportunidades", "funis", nameof(RegistroDeOportunidade.FunilId), "funil_id");
        oportunidades
            .HasIndex(registro => new { registro.ConstrutoraId, registro.Responsavel, registro.Situacao })
            .HasDatabaseName("ix_oportunidades_construtora_id_responsavel_situacao");
        Referenciavel(oportunidades, "oportunidades");

        var atividades = Agregado<RegistroDeAtividade>(modelBuilder, "atividades");
        atividades.Property(registro => registro.Responsavel).HasColumnName("responsavel").HasMaxLength(100);
        atividades.Property(registro => registro.PrevistaPara).HasColumnName("prevista_para");
        ComSituacao(atividades, "atividades", registro => registro.Situacao);
        Referencia<RegistroDeAtividade, RegistroDeConta>(atividades, "atividades", "contas", nameof(RegistroDeAtividade.ContaId), "conta_id");
        Referencia<RegistroDeAtividade, RegistroDeOportunidade>(atividades, "atividades", "oportunidades", nameof(RegistroDeAtividade.OportunidadeId), "oportunidade_id");

        // A agenda: as pendentes de um vendedor, pela hora.
        atividades
            .HasIndex(registro => new { registro.ConstrutoraId, registro.Responsavel, registro.PrevistaPara })
            .HasFilter("situacao = 'Pendente'")
            .HasDatabaseName("ix_atividades_pendentes_por_responsavel");

        var propostas = Agregado<RegistroDeProposta>(modelBuilder, "propostas");
        Referencia<RegistroDeProposta, RegistroDeOportunidade>(propostas, "propostas", "oportunidades", nameof(RegistroDeProposta.OportunidadeId), "oportunidade_id");
        Referencia<RegistroDeProposta, RegistroDeConta>(propostas, "propostas", "contas", nameof(RegistroDeProposta.ContaId), "conta_id");
    }

    /// <summary>As colunas que toda tabela de agregado tem, e o filtro pela construtora.</summary>
    private EntityTypeBuilder<T> Agregado<T>(ModelBuilder modelBuilder, string tabela)
        where T : RegistroDeAgregado
    {
        var entidade = modelBuilder.Entity<T>();

        entidade.ToTable(tabela, tabelaBuilder =>
        {
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_versao", "versao >= 1");
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_formato", "formato >= 1");
            tabelaBuilder.HasCheckConstraint($"ck_{tabela}_documento", "jsonb_typeof(documento) = 'object'");
        });

        entidade.HasKey(registro => registro.Id).HasName($"pk_{tabela}");
        entidade.Property(registro => registro.Id).HasColumnName("id").ValueGeneratedNever();
        entidade.Property(registro => registro.ConstrutoraId).HasColumnName("construtora_id");
        entidade.Property(registro => registro.Versao).HasColumnName("versao").IsConcurrencyToken();
        entidade.Property(registro => registro.Formato).HasColumnName("formato");
        entidade.Property(registro => registro.Documento).HasColumnName("documento").HasColumnType("jsonb");
        entidade.Property(registro => registro.CriadoEm).HasColumnName("criado_em");
        entidade.Property(registro => registro.AtualizadoEm).HasColumnName("atualizado_em");

        entidade.HasQueryFilter(FiltroDaConstrutora, registro => registro.ConstrutoraId == ConstrutoraId);

        return entidade;
    }

    /// <summary>A coluna <c>nome</c>, indexada com a construtora — para listar e procurar sem abrir documento.</summary>
    private static void ComNome<T>(EntityTypeBuilder<T> entidade, string tabela, int maiorNome)
        where T : RegistroComNome
    {
        entidade.Property(registro => registro.Nome).HasColumnName("nome").HasMaxLength(maiorNome);
        entidade.HasIndex(registro => new { registro.ConstrutoraId, registro.Nome }).HasDatabaseName($"ix_{tabela}_construtora_id_nome");
    }

    /// <summary>A coluna <c>situacao</c>, em texto, só com os valores que o enum tem.</summary>
    private static void ComSituacao<T, TSituacao>(EntityTypeBuilder<T> entidade, string tabela, Expression<Func<T, TSituacao>> situacao)
        where T : RegistroDeAgregado
        where TSituacao : struct, Enum
    {
        entidade.Property(situacao).HasColumnName("situacao").HasConversion<string>().HasMaxLength(20);
        entidade.ToTable(tabelaBuilder => tabelaBuilder.HasCheckConstraint(
            $"ck_{tabela}_situacao",
            $"situacao in ({string.Join(", ", Enum.GetNames<TSituacao>().Select(nome => $"'{nome}'"))})"));
    }

    /// <summary>
    /// A chave que as referências de outros agregados usam: o id sozinho já é
    /// único, mas é o par que amarra a referência à mesma construtora.
    /// </summary>
    private static void Referenciavel<T>(EntityTypeBuilder<T> entidade, string tabela)
        where T : RegistroDeAgregado =>
        entidade.HasAlternateKey(registro => new { registro.ConstrutoraId, registro.Id }).HasName($"ak_{tabela}_construtora_id_id");

    /// <summary>
    /// Uma referência a outro agregado, da mesma construtora: chave
    /// estrangeira composta, que não deixa o referenciado sair do banco
    /// (<c>RESTRICT</c>), e indexada para achar quem aponta para ele.
    /// </summary>
    private static void Referencia<T, TReferenciado>(
        EntityTypeBuilder<T> entidade,
        string tabela,
        string tabelaReferenciada,
        string propriedade,
        string coluna)
        where T : RegistroDeAgregado
        where TReferenciado : RegistroDeAgregado
    {
        entidade.Property(propriedade).HasColumnName(coluna);

        entidade
            .HasIndex(nameof(RegistroDeAgregado.ConstrutoraId), propriedade)
            .HasDatabaseName($"ix_{tabela}_construtora_id_{coluna}");
        entidade
            .HasOne<TReferenciado>()
            .WithMany()
            .HasForeignKey(nameof(RegistroDeAgregado.ConstrutoraId), propriedade)
            .HasPrincipalKey(nameof(RegistroDeAgregado.ConstrutoraId), nameof(RegistroDeAgregado.Id))
            .HasConstraintName($"fk_{tabela}_{tabelaReferenciada}")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
