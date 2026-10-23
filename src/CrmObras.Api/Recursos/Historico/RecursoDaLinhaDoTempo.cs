using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Historico;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Historico;

/// <summary>Uma coisa que aconteceu com o cliente, numa frase — e a venda dela, se é de uma.</summary>
public sealed record AcontecimentoResposta(DateTimeOffset Em, string Descricao, Guid? Oportunidade);

/// <summary>A história do cliente, do mais recente para o mais antigo: para quem assumir a conta.</summary>
public sealed class RecursoDaLinhaDoTempo : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        api.MapGet("/contas/{id:guid}/linha-do-tempo", LinhaDaConta).WithTags("Contas").RequireAuthorization(Politicas.Consultar);
    }

    private static async Task<Ok<IReadOnlyList<AcontecimentoResposta>>> LinhaDaConta(
        Guid id,
        RepositorioDeContas contas,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades oportunidades,
        RepositorioDeAtividades atividades,
        RepositorioDePropostas propostas,
        CancellationToken cancelamento)
    {
        var conta = (await Leituras.Conta(contas, id, cancelamento)).Agregado;
        var vendas = (await oportunidades.DaConta(conta.Id, cancelamento)).Select(lida => lida.Agregado).ToList();
        List<Funil> dasVendas = [];

        foreach (var funil in vendas.Select(venda => venda.Funil).Distinct())
        {
            dasVendas.Add((await Leituras.Funil(funis, funil.Valor, cancelamento)).Agregado);
        }

        var linha = LinhaDoTempo.Da(
            conta,
            dasVendas,
            vendas,
            (await atividades.DaConta(conta.Id, cancelamento)).Select(lida => lida.Agregado),
            (await propostas.DaConta(conta.Id, cancelamento)).Select(lida => lida.Agregado));

        return TypedResults.Ok<IReadOnlyList<AcontecimentoResposta>>(
            [.. linha.Select(acontecimento => new AcontecimentoResposta(acontecimento.Em, acontecimento.Descricao, acontecimento.Oportunidade?.Valor))]);
    }
}
