using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;

namespace CrmObras.Dominio.Testes.Identificacao;

public sealed class DocumentoTests
{
    [Fact]
    public void Decide_entre_cpf_e_cnpj_pelo_tamanho()
    {
        Assert.IsType<Cnpj>(Documento.De("11.222.333/0001-81"));
        Assert.IsType<Cnpj>(Documento.De("12.ABC.345/01DE-35"));
        Assert.IsType<Cpf>(Documento.De("529.982.247-25"));
    }

    [Fact]
    public void O_que_nao_tem_tamanho_de_um_nem_de_outro_e_recusado_dizendo_os_dois_tamanhos()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Documento.De("123.456"));

        Assert.Equal("\"123.456\" não é CPF nem CNPJ: o CPF tem 11 dígitos, e o CNPJ, 14 caracteres.", erro.Message);
    }

    [Fact]
    public void Documentos_iguais_sao_iguais_escritos_de_qualquer_jeito()
    {
        Assert.Equal(Documento.De("11.222.333/0001-81"), Cnpj.De("11222333000181"));
        Assert.Equal(Documento.De("529.982.247-25"), Cpf.De("52998224725"));
    }
}
