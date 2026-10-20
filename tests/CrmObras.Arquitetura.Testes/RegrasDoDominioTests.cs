using System.Reflection;
using System.Runtime.CompilerServices;
using CrmObras.Dominio;

namespace CrmObras.Arquitetura.Testes;

/*
 * Regras de arquitetura verificadas por teste, não por convenção. Quem
 * acrescentar uma referência ao Entity Framework no domínio "só para resolver
 * rápido" descobre no build, e não seis meses depois.
 */
public sealed class RegrasDoDominioTests
{
    private static readonly Assembly Dominio = typeof(ReferenciaDoAssembly).Assembly;

    [Fact]
    public void O_dominio_depende_apenas_da_biblioteca_padrao()
    {
        /*
         * Nem pacote de terceiro, nem projeto da própria solução: é o que mantém
         * as regras do relacionamento com o cliente testáveis sem banco, sem
         * servidor e sem rede.
         */
        var externas = Dominio
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name ?? string.Empty)
            .Where(nome => !EhDaBibliotecaPadrao(nome))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(externas);
    }

    [Fact]
    public void As_classes_do_dominio_sao_seladas()
    {
        /*
         * Herança é um ponto de extensão, e ponto de extensão precisa ser
         * projetado: classe aberta por omissão convida a sobrescrever o que a
         * entidade assume ser fixo. Classe estática conta como selada — o
         * compilador a emite como `abstract sealed`.
         */
        var abertas = Dominio
            .GetExportedTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsSealed && !tipo.IsAbstract)
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.Empty(abertas);
    }

    [Fact]
    public void O_estado_do_dominio_nao_tem_setter_publico()
    {
        /*
         * O estado muda por método com nome de negócio — Qualificar,
         * DefinirPrincipal —, que valida antes. Um setter público deixaria
         * qualquer camada escrever um valor que o método recusaria. `init`
         * é permitido: só vale na construção, onde a validação acontece.
         */
        var comSetterPublico = Dominio
            .GetExportedTypes()
            .SelectMany(tipo => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(propriedade => propriedade.SetMethod is { IsPublic: true } setter && !EhInit(setter))
            .Select(propriedade => $"{propriedade.DeclaringType?.Name}.{propriedade.Name}")
            .ToArray();

        Assert.Empty(comSetterPublico);
    }

    [Fact]
    public void Os_valores_do_dominio_nao_tem_construtor_publico()
    {
        /*
         * CNPJ, CPF, e-mail e telefone só nascem pela leitura — `Cnpj.De` —,
         * que valida. Um construtor público faria um CNPJ que não confere.
         */
        var valores = Dominio
            .GetExportedTypes()
            .Where(tipo => tipo.Namespace is "CrmObras.Dominio.Identificacao" or "CrmObras.Dominio.MeiosDeContato")
            .ToList();

        Assert.NotEmpty(valores);
        Assert.All(valores, valor => Assert.Empty(valor.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));
    }

    private static bool EhDaBibliotecaPadrao(string nome) =>
        nome is "System" or "netstandard" or "mscorlib"
        || nome.StartsWith("System.", StringComparison.Ordinal);

    private static bool EhInit(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
}
