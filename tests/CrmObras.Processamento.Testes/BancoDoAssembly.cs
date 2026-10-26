using CrmObras.Infraestrutura.Testes.Persistencia;
using Xunit.Sdk;
using Xunit.v3;

// Um banco para o assembly de testes inteiro, como nos testes da
// infraestrutura: sobe uma vez, antes do primeiro teste.
[assembly: AssemblyFixture(typeof(BancoDeTestes))]

// A caixa de saída é uma só para o banco inteiro, e a fila reserva a próxima
// mensagem de qualquer construtora: dois testes ao mesmo tempo tratariam as
// mensagens um do outro. Aqui, um teste de cada vez.
[assembly: Parallelization(Mode = ParallelMode.None)]
