using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmObras.Infraestrutura.Persistencia.Migracoes
{
    /// <summary>
    /// O esquema do CRM: uma tabela por agregado, cada linha um documento
    /// <c>jsonb</c> com a construtora dona dele — e a segurança por linha, que
    /// só deixa cada conexão ver e gravar as linhas da construtora que ela
    /// declarou (ADR-0011).
    /// </summary>
    public partial class Inicial : Migration
    {
        /// <summary>As tabelas com dados de construtora — todas as do esquema, menos o histórico de migrações.</summary>
        private static readonly string[] TabelasDeConstrutora = ["contas", "leads", "funis", "oportunidades", "atividades", "propostas"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "contas",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj_ou_cpf = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                    table.UniqueConstraint("ak_contas_construtora_id_id", x => new { x.construtora_id, x.id });
                    table.CheckConstraint("ck_contas_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_contas_formato", "formato >= 1");
                    table.CheckConstraint("ck_contas_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "funis",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_funis", x => x.id);
                    table.UniqueConstraint("ak_funis_construtora_id_id", x => new { x.construtora_id, x.id });
                    table.CheckConstraint("ck_funis_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_funis_formato", "formato >= 1");
                    table.CheckConstraint("ck_funis_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "leads",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.CheckConstraint("ck_leads_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_leads_formato", "formato >= 1");
                    table.CheckConstraint("ck_leads_situacao", "situacao in ('Novo', 'EmQualificacao', 'Qualificado', 'Descartado', 'Convertido')");
                    table.CheckConstraint("ck_leads_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "oportunidades",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    responsavel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oportunidades", x => x.id);
                    table.UniqueConstraint("ak_oportunidades_construtora_id_id", x => new { x.construtora_id, x.id });
                    table.CheckConstraint("ck_oportunidades_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_oportunidades_formato", "formato >= 1");
                    table.CheckConstraint("ck_oportunidades_situacao", "situacao in ('Aberta', 'Ganha', 'Perdida')");
                    table.CheckConstraint("ck_oportunidades_versao", "versao >= 1");
                    table.ForeignKey(
                        name: "fk_oportunidades_contas",
                        columns: x => new { x.construtora_id, x.conta_id },
                        principalSchema: "crm",
                        principalTable: "contas",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_oportunidades_funis",
                        columns: x => new { x.construtora_id, x.funil_id },
                        principalSchema: "crm",
                        principalTable: "funis",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "atividades",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oportunidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsavel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prevista_para = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_atividades", x => x.id);
                    table.CheckConstraint("ck_atividades_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_atividades_formato", "formato >= 1");
                    table.CheckConstraint("ck_atividades_situacao", "situacao in ('Pendente', 'Concluida', 'Cancelada')");
                    table.CheckConstraint("ck_atividades_versao", "versao >= 1");
                    table.ForeignKey(
                        name: "fk_atividades_contas",
                        columns: x => new { x.construtora_id, x.conta_id },
                        principalSchema: "crm",
                        principalTable: "contas",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_atividades_oportunidades",
                        columns: x => new { x.construtora_id, x.oportunidade_id },
                        principalSchema: "crm",
                        principalTable: "oportunidades",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "propostas",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oportunidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    construtora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<short>(type: "smallint", nullable: false),
                    documento = table.Column<string>(type: "jsonb", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_propostas", x => x.id);
                    table.CheckConstraint("ck_propostas_documento", "jsonb_typeof(documento) = 'object'");
                    table.CheckConstraint("ck_propostas_formato", "formato >= 1");
                    table.CheckConstraint("ck_propostas_versao", "versao >= 1");
                    table.ForeignKey(
                        name: "fk_propostas_contas",
                        columns: x => new { x.construtora_id, x.conta_id },
                        principalSchema: "crm",
                        principalTable: "contas",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_propostas_oportunidades",
                        columns: x => new { x.construtora_id, x.oportunidade_id },
                        principalSchema: "crm",
                        principalTable: "oportunidades",
                        principalColumns: new[] { "construtora_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_atividades_construtora_id_conta_id",
                schema: "crm",
                table: "atividades",
                columns: new[] { "construtora_id", "conta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_atividades_construtora_id_oportunidade_id",
                schema: "crm",
                table: "atividades",
                columns: new[] { "construtora_id", "oportunidade_id" });

            migrationBuilder.CreateIndex(
                name: "ix_atividades_pendentes_por_responsavel",
                schema: "crm",
                table: "atividades",
                columns: new[] { "construtora_id", "responsavel", "prevista_para" },
                filter: "situacao = 'Pendente'");

            migrationBuilder.CreateIndex(
                name: "ix_contas_construtora_id_nome",
                schema: "crm",
                table: "contas",
                columns: new[] { "construtora_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ux_contas_construtora_id_cnpj_ou_cpf",
                schema: "crm",
                table: "contas",
                columns: new[] { "construtora_id", "cnpj_ou_cpf" },
                unique: true,
                filter: "cnpj_ou_cpf is not null");

            migrationBuilder.CreateIndex(
                name: "ix_funis_construtora_id_nome",
                schema: "crm",
                table: "funis",
                columns: new[] { "construtora_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_construtora_id_nome",
                schema: "crm",
                table: "leads",
                columns: new[] { "construtora_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_oportunidades_construtora_id_conta_id",
                schema: "crm",
                table: "oportunidades",
                columns: new[] { "construtora_id", "conta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_oportunidades_construtora_id_funil_id",
                schema: "crm",
                table: "oportunidades",
                columns: new[] { "construtora_id", "funil_id" });

            migrationBuilder.CreateIndex(
                name: "ix_oportunidades_construtora_id_responsavel_situacao",
                schema: "crm",
                table: "oportunidades",
                columns: new[] { "construtora_id", "responsavel", "situacao" });

            migrationBuilder.CreateIndex(
                name: "ix_propostas_construtora_id_conta_id",
                schema: "crm",
                table: "propostas",
                columns: new[] { "construtora_id", "conta_id" });

            migrationBuilder.CreateIndex(
                name: "ix_propostas_construtora_id_oportunidade_id",
                schema: "crm",
                table: "propostas",
                columns: new[] { "construtora_id", "oportunidade_id" });

            /*
             * Segurança por linha: cada conexão só vê e só grava as linhas da
             * construtora que ela declarou em crm.construtora_id. Sem
             * construtora declarada, não vê nada. FORCE vale a regra também
             * para o dono das tabelas; só superusuário e papel com BYPASSRLS
             * passam por cima — que é como as migrações rodam.
             */
            foreach (var tabela in TabelasDeConstrutora)
            {
                migrationBuilder.Sql($"""
                    alter table crm.{tabela} enable row level security;
                    alter table crm.{tabela} force row level security;
                    create policy isolamento_por_construtora on crm.{tabela}
                        using (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid)
                        with check (construtora_id = nullif(current_setting('crm.construtora_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "atividades",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "leads",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "propostas",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "oportunidades",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "contas",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "funis",
                schema: "crm");
        }
    }
}
