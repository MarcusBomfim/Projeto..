using CrmObras.Dominio.Comum;

namespace CrmObras.Infraestrutura.Persistencia;

/// <summary>
/// Um agregado lido do banco, e a versão em que ele estava.
/// </summary>
/// <remarks>
/// A versão fica aqui, ao lado do agregado, e não dentro dele: é coisa da
/// persistência, e não do negócio. Quem altera e grava de volta devolve a
/// versão que leu — se outro gravou no meio, a gravação é recusada
/// (ADR-0012).
/// </remarks>
public sealed record Versionado<T>(T Agregado, int Versao);

/// <summary>O que se lista de um agregado sem abrir o documento dele.</summary>
public sealed record ResumoDoAgregado<TId>(TId Id, string Nome, int Versao, DateTimeOffset AtualizadoEm)
    where TId : Identificador;
