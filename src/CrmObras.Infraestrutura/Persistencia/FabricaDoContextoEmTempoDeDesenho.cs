using Microsoft.EntityFrameworkCore.Design;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// O contexto que o dotnet-ef usa para gerar e aplicar migrações. Gerar
/// migração não abre conexão: a cadeia de conexão aqui só diz que o banco é
/// PostgreSQL. Aplicar abre, e a cadeia vem de <c>--connection</c> ou da
/// variável de ambiente. E não lê nem grava agregado, por isso nenhuma
/// construtora.
/// </summary>
public sealed class FabricaDoContextoEmTempoDeDesenho : IDesignTimeDbContextFactory<ContextoDoBanco>
{
    public ContextoDoBanco CreateDbContext(string[] args) =>
        ContextoDoBanco.Criar(
            Environment.GetEnvironmentVariable("ConnectionStrings__Crm") ?? "Host=localhost;Database=crm_obras",
            ConstrutoraFixa.Nenhuma);
}
