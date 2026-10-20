using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;

namespace CrmObras.Dominio.Testes.Clientes;

public sealed class IdentificadoresTests
{
    [Fact]
    public void O_guid_vazio_nao_e_identidade()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => new ContaId(Guid.Empty));

        Assert.Equal("O identificador não pode ser vazio.", erro.Message);
    }

    [Fact]
    public void Ids_de_tipos_diferentes_nunca_sao_iguais_nem_com_o_mesmo_guid()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(new ContaId(guid), new ContaId(guid));
        Assert.NotEqual<Identificador>(new ContaId(guid), new ContatoId(guid));
        Assert.Equal(guid.ToString(), new ContaId(guid).ToString());
    }

    [Fact]
    public void Os_ids_novos_saem_em_ordem_de_criacao()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => ContaId.Novo().Valor).ToList();

        Assert.All(ids, id => Assert.Equal(7, id.Version));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
