using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CrmObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// A caixa de saída dos fatos do domínio, com a segurança por linha das
    /// outras tabelas, as funções que atravessam construtoras — cada uma para
    /// uma coisa só — e o aviso de mensagem nova (ADR-0016).
    /// </summary>
    public partial class CaixaDeSaida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caixa_de_saida",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    conteudo = table.Column<string>(type: "jsonb", nullable: false),
                    agregado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao_do_agregado = table.Column<int>(type: "integer", nullable: false),
                    ocorrida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    proxima_tentativa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tratada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    abandonada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_erro = table.Column<string>(type: "text", nullable: true),
                    contexto_do_rastreio = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixa_de_saida", x => x.id);
                    table.CheckConstraint("ck_caixa_de_saida_conteudo", "jsonb_typeof(conteudo) = 'object'");
                    table.CheckConstraint("ck_caixa_de_saida_formato", "formato >= 1");
                    table.CheckConstraint("ck_caixa_de_saida_tentativas", "tentativas >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_caixa_de_saida_pendentes",
                schema: "crm",
                table: "caixa_de_saida",
                columns: new[] { "proxima_tentativa_em", "sequencia" },
                filter: "tratada_em is null and abandonada_em is null");

            migrationBuilder.CreateIndex(
                name: "ix_caixa_de_saida_tratada_em",
                schema: "crm",
                table: "caixa_de_saida",
                column: "tratada_em",
                filter: "tratada_em is not null");

            // Segurança por linha, como nas outras tabelas (ADR-0011).
            migrationBuilder.Sql("""
                alter table crm.caixa_de_saida enable row level security;
                alter table crm.caixa_de_saida force row level security;
                create policy isolamento_por_construtora on crm.caixa_de_saida
                    using (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid)
                    with check (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid);
                """);

            /*
             * As únicas portas que atravessam construtoras, e cada uma faz uma
             * coisa só. Rodam com os direitos de quem as criou — o papel das
             * migrações, que passa por cima da segurança por linha —, e com o
             * search_path fixo, para ninguém pôr outra tabela no lugar.
             *
             * A reserva entrega só o id e a construtora da próxima mensagem, e
             * a trava até o fim da transação de quem chamou: o conteúdo, cada
             * um lê o seu, em nome da construtora dele. As limpezas só apagam
             * o que é anterior a uma data.
             *
             * E não são de todo mundo: o PostgreSQL dá EXECUTE a PUBLIC em
             * toda função nova, e aqui isso é revogado. Quem implanta concede
             * ao papel do processador — a API, que está na frente da
             * internet, não alcança nada que atravesse construtoras.
             */
            migrationBuilder.Sql("""
                create function crm.reservar_mensagens(limite integer, agora timestamptz)
                returns table (id uuid, construtora_id uuid)
                language sql
                security definer
                set search_path = crm, pg_temp
                as $$
                    select m.id, m.construtora_id
                    from crm.caixa_de_saida m
                    where m.tratada_em is null and m.abandonada_em is null and m.proxima_tentativa_em <= agora
                    order by m.proxima_tentativa_em, m.sequencia
                    limit limite
                    for update skip locked
                $$;

                create function crm.apagar_mensagens_tratadas_antes_de(limite timestamptz)
                returns integer
                language sql
                security definer
                set search_path = crm, pg_temp
                as $$
                    with apagadas as (delete from crm.caixa_de_saida where tratada_em < limite returning 1)
                    select count(*)::integer from apagadas
                $$;

                create function crm.apagar_chaves_de_idempotencia_antes_de(limite timestamptz)
                returns integer
                language sql
                security definer
                set search_path = crm, pg_temp
                as $$
                    with apagadas as (delete from crm.chaves_de_idempotencia where criada_em < limite returning 1)
                    select count(*)::integer from apagadas
                $$;

                revoke execute on function crm.reservar_mensagens(integer, timestamptz) from public;
                revoke execute on function crm.apagar_mensagens_tratadas_antes_de(timestamptz) from public;
                revoke execute on function crm.apagar_chaves_de_idempotencia_antes_de(timestamptz) from public;
                """);

            // Mensagem nova acorda quem escuta, na hora em que a transação que
            // a gravou é confirmada — e não na próxima volta do processador.
            migrationBuilder.Sql("""
                create function crm.avisar_mensagem_nova()
                returns trigger
                language plpgsql
                as $$
                begin
                    perform pg_notify('crm_caixa_de_saida', '');
                    return null;
                end
                $$;

                create trigger avisar_mensagem_nova
                    after insert on crm.caixa_de_saida
                    for each statement
                    execute function crm.avisar_mensagem_nova();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                drop trigger avisar_mensagem_nova on crm.caixa_de_saida;
                drop function crm.avisar_mensagem_nova();
                drop function crm.apagar_chaves_de_idempotencia_antes_de(timestamptz);
                drop function crm.apagar_mensagens_tratadas_antes_de(timestamptz);
                drop function crm.reservar_mensagens(integer, timestamptz);
                """);

            migrationBuilder.DropTable(
                name: "caixa_de_saida",
                schema: "crm");
        }
    }
}
