using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// O que a API precisa da persistência: as chaves de idempotência de cada
    /// construtora, com a segurança por linha das outras tabelas (ADR-0013), e
    /// o nome comparável das contas, para a busca de duplicados achar no banco
    /// a conta de mesmo nome (ADR-0003).
    /// </summary>
    /// <remarks>
    /// O nome comparável entra anulável: é o domínio que o calcula, e não o
    /// SQL. As contas gravadas antes desta migração o ganham na próxima
    /// gravação; até lá, só não são achadas pelo nome.
    /// </remarks>
    public partial class ChavesDeIdempotenciaENomeComparavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "nome_comparavel",
                schema: "crm",
                table: "contas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chaves_de_idempotencia",
                schema: "crm",
                columns: table => new
                {
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    impressao_do_pedido = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: true),
                    cabecalhos = table.Column<string>(type: "jsonb", nullable: true),
                    corpo = table.Column<string>(type: "text", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chaves_de_idempotencia", x => new { x.construtora_id, x.chave });
                    table.CheckConstraint("ck_chaves_de_idempotencia_chave", "length(chave) between 1 and 255");
                });

            migrationBuilder.CreateIndex(
                name: "ix_contas_construtora_id_nome_comparavel",
                schema: "crm",
                table: "contas",
                columns: new[] { "construtora_id", "nome_comparavel" });

            migrationBuilder.Sql("""
                alter table crm.chaves_de_idempotencia enable row level security;
                alter table crm.chaves_de_idempotencia force row level security;
                create policy isolamento_por_construtora on crm.chaves_de_idempotencia
                    using (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid)
                    with check (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chaves_de_idempotencia",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "ix_contas_construtora_id_nome_comparavel",
                schema: "crm",
                table: "contas");

            migrationBuilder.DropColumn(
                name: "nome_comparavel",
                schema: "crm",
                table: "contas");
        }
    }
}
