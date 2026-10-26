using CrmObras.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CrmObras.Infraestrutura.Testes.Persistencia;

public sealed class ModeloTests
{
    [Fact]
    public void Toda_mudanca_no_modelo_tem_migracao()
    {
        /*
         * Mudou o mapeamento e esqueceu de gerar a migração: o código espera
         * uma coluna que o banco não tem. O EF compara o modelo com o
         * instantâneo da última migração — sem abrir conexão — e este teste
         * falha antes de alguém descobrir em produção.
         */
        using var contexto = new FabricaDoContextoEmTempoDeDesenho().CreateDbContext([]);

        Assert.False(contexto.Database.HasPendingModelChanges(), "Há mudança no modelo sem migração: rode dotnet ef migrations add.");
    }

    [Fact]
    public void Toda_tabela_e_filtrada_pela_construtora_tambem_no_codigo()
    {
        /*
         * Tabela nova sem o filtro passaria nos testes de isolamento: a
         * segurança por linha filtraria sozinha. E ficaria com uma barreira
         * só, das duas que a ADR-0011 promete.
         */
        using var contexto = new FabricaDoContextoEmTempoDeDesenho().CreateDbContext([]);

        Assert.Equal(8, contexto.Model.GetEntityTypes().Count());
        Assert.All(
            contexto.Model.GetEntityTypes(),
            entidade => Assert.True(
                entidade.FindDeclaredQueryFilter(ContextoDoBanco.FiltroDaConstrutora) is not null,
                $"{entidade.ClrType.Name} sem o filtro da construtora."));
    }
}
