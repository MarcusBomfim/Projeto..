using CrmObras.Infraestrutura.Testes.Persistencia;

// Um banco para o assembly de testes inteiro: sobe uma vez, antes do
// primeiro teste que o pede.
[assembly: AssemblyFixture(typeof(BancoDeTestes))]
