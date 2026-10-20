using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Identificacao;

namespace CrmObras.Dominio.Testes.Identificacao;

/*
 * Propriedades em documentos sorteados, com semente fixa: os verificadores
 * são calculados aqui, por uma conta escrita à parte — os pesos numa tabela,
 * e não no laço do domínio —, e o domínio tem de concordar.
 */
public sealed class DocumentosSorteadosTests
{
    private const int Casos = 400;

    private const string CaracteresDaBase = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static readonly int[] PesosDoCnpj = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private static readonly int[] PesosDoCpf = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

    [Fact]
    public void Todo_cnpj_com_os_verificadores_certos_e_aceito_e_os_errados_recusados()
    {
        var sorteio = new Random(20261019);

        for (var caso = 0; caso < Casos; caso++)
        {
            // Metade numérica, como os de antes; metade com letras.
            var alfabeto = caso % 2 == 0 ? CaracteresDaBase[..10] : CaracteresDaBase;
            var raiz = new string([.. Enumerable.Range(0, 12).Select(_ => alfabeto[sorteio.Next(alfabeto.Length)])]);
            var numero = ComVerificadores(raiz, PesosDoCnpj);

            var cnpj = Cnpj.De(numero);
            Assert.Equal(numero, cnpj.Numero);
            Assert.Equal(cnpj, Cnpj.De(cnpj.Formatado));

            var verificadorErrado = (char)('0' + ((numero[13] - '0' + 1 + sorteio.Next(9)) % 10));
            Assert.Throws<ExcecaoDeDominio>(() => Cnpj.De(numero[..13] + verificadorErrado));
        }
    }

    [Fact]
    public void Todo_cpf_com_os_verificadores_certos_e_aceito_e_os_errados_recusados()
    {
        var sorteio = new Random(20261019);

        for (var caso = 0; caso < Casos; caso++)
        {
            var raiz = string.Concat(Enumerable.Range(0, 9).Select(_ => sorteio.Next(10)));

            if (raiz.All(digito => digito == raiz[0]))
            {
                continue;
            }

            var numero = ComVerificadores(raiz, PesosDoCpf);

            Assert.Equal(numero, Cpf.De(numero).Numero);
            Assert.Equal(Cpf.De(numero), Documento.De(Cpf.De(numero).Formatado));

            var verificadorErrado = (char)('0' + ((numero[10] - '0' + 1 + sorteio.Next(9)) % 10));
            Assert.Throws<ExcecaoDeDominio>(() => Cpf.De(numero[..10] + verificadorErrado));
        }
    }

    /// <summary>Os dois verificadores, cada um pelos últimos pesos da tabela: o segundo conta o primeiro.</summary>
    private static string ComVerificadores(string raiz, int[] pesos)
    {
        var numero = raiz;

        for (var verificador = 0; verificador < 2; verificador++)
        {
            var pesosDaVez = pesos[^numero.Length..];
            var soma = numero.Select((caractere, posicao) => (caractere - '0') * pesosDaVez[posicao]).Sum();
            var resto = soma % 11;
            numero += resto < 2 ? "0" : (11 - resto).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return numero;
    }
}
