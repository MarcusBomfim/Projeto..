using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Leads;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Arquitetura.Testes;

public sealed class IdentidadeDosAgregadosTests
{
    [Fact]
    public void Todo_agregado_tem_identidade_propria()
    {
        /*
         * Raiz de agregado é o que a persistência vai gravar e a API endereçar:
         * tem um Id do seu próprio tipo de identificador, e não um Guid solto.
         */
        Type[] raizes = [typeof(Conta), typeof(Lead), typeof(Funil), typeof(Oportunidade)];

        var semIdentidade = raizes
            .Where(raiz => raiz.GetProperty("Id")?.PropertyType is not { } tipo
                || !tipo.IsSubclassOf(typeof(Identificador))
                || !tipo.Name.StartsWith(raiz.Name, StringComparison.Ordinal))
            .Select(raiz => raiz.Name);

        Assert.Empty(semIdentidade);
    }
}
