using Microsoft.AspNetCore.Http.HttpResults;
using CrmObras.Api.Http;
using CrmObras.Api.Seguranca;
using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;
using CrmObras.Infraestrutura.Persistencia.Repositorios;

namespace CrmObras.Api.Recursos.Oportunidades;

/// <summary>As vendas: a abertura, o andar pelo funil, o ganho e a perda — e o quadro do funil (ADR-0005).</summary>
public sealed class RecursoDeOportunidades : IRecursoDaApi
{
    public void Mapear(IEndpointRouteBuilder api)
    {
        var vendas = api.MapGroup("/oportunidades").WithTags("Oportunidades");

        vendas.MapGet("/", Listar).RequireAuthorization(Politicas.Consultar);
        vendas.MapPost("/", Criar).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        vendas.MapPost("/do-lead", AbrirPeloLead).RequireAuthorization(Politicas.Vender).ComIdempotencia();
        vendas.MapGet("/{id:guid}", Obter).RequireAuthorization(Politicas.Consultar);
        vendas.MapPatch("/{id:guid}", AlterarVenda).RequireAuthorization(Politicas.Vender);
        vendas.MapDelete("/{id:guid}", Remover).RequireAuthorization(Politicas.Gerir);

        vendas.MapPut("/{id:guid}/responsavel", Transferir).RequireAuthorization(Politicas.Gerir);
        vendas.MapPut("/{id:guid}/etapa", Mover).RequireAuthorization(Politicas.Vender);
        vendas.MapPost("/{id:guid}/ganho", Ganhar).RequireAuthorization(Politicas.Vender);
        vendas.MapPost("/{id:guid}/perda", Perder).RequireAuthorization(Politicas.Vender);
        vendas.MapPost("/{id:guid}/reabertura", Reabrir).RequireAuthorization(Politicas.Vender);

        api.MapGet("/funis/{id:guid}/quadro", Quadro).WithTags("Oportunidades").RequireAuthorization(Politicas.Consultar);
    }

    /// <summary>As vendas de um funil — <c>?funil=</c> — ou de uma conta — <c>?conta=</c> —, da mais antiga.</summary>
    private static async Task<Ok<IReadOnlyList<ResumoDaOportunidadeResposta>>> Listar(
        Guid? funil,
        Guid? conta,
        RepositorioDeOportunidades repositorio,
        CancellationToken cancelamento)
    {
        var lidas = (funil, conta) switch
        {
            ({ } doFunil, null) => await repositorio.DoFunil(new FunilId(doFunil), cancelamento),
            (null, { } daConta) => await repositorio.DaConta(new ContaId(daConta), cancelamento),
            _ => throw new PedidoInvalido("Diga de onde são as vendas: ?funil= ou ?conta=, um dos dois."),
        };

        return TypedResults.Ok<IReadOnlyList<ResumoDaOportunidadeResposta>>([.. lidas.Select(ResumoDaOportunidadeResposta.De)]);
    }

    private static async Task<Created<OportunidadeResposta>> Criar(
        NovaOportunidade nova,
        RepositorioDeOportunidades repositorio,
        RepositorioDeContas contas,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var conta = (await Leituras.Conta(contas, nova.Conta, cancelamento)).Agregado;
        var funil = (await Leituras.Funil(funis, nova.Funil, cancelamento)).Agregado;

        var venda = new Oportunidade(conta.Id, nova.Titulo, funil, QuemPede.Login(http.User), fuso.Agora());

        if (nova.ValorEstimado is { } valor)
        {
            venda.Estimar(Dinheiro.Reais(valor));
        }

        if (nova.PrevisaoDeFechamento is { } previsao)
        {
            venda.PreverFechamento(previsao);
        }

        if (nova.QuemDecide is { } quemDecide)
        {
            venda.DefinirQuemDecide(conta, new ContatoId(quemDecide));
        }

        return await Gravar(venda, repositorio, http, cancelamento);
    }

    /// <summary>
    /// A venda do lead convertido: na conta em que ele virou cliente, com a
    /// obra que ele descreveu de título, e quem decide, se a qualificação
    /// disse que é ele.
    /// </summary>
    private static async Task<Created<OportunidadeResposta>> AbrirPeloLead(
        OportunidadeDoLead pedido,
        RepositorioDeOportunidades repositorio,
        RepositorioDeLeads leads,
        RepositorioDeContas contas,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lead = (await Leituras.Lead(leads, pedido.Lead, cancelamento)).Agregado;
        var funil = (await Leituras.Funil(funis, pedido.Funil, cancelamento)).Agregado;

        // Sem conta, não há onde abrir a venda: o lead ainda não virou cliente.
        var conta = lead.Conta is { } daConversao
            ? (await Leituras.Conta(contas, daConversao.Valor, cancelamento)).Agregado
            : throw new ExcecaoDeDominio($"O lead {lead.Nome} ainda não virou cliente: converta-o antes de abrir a venda.");

        return await Gravar(AberturaPeloLead.Abrir(lead, conta, funil, QuemPede.Login(http.User), fuso.Agora()), repositorio, http, cancelamento);
    }

    private static async Task<Results<Ok<OportunidadeResposta>, StatusCodeHttpResult>> Obter(
        Guid id,
        RepositorioDeOportunidades repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Oportunidade(repositorio, id, cancelamento);
        Etiquetas.Escrever(http, lida.Versao);

        return Etiquetas.ClienteJaTem(http, lida.Versao)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.Ok(OportunidadeResposta.De(lida.Agregado));
    }

    private static Task<Ok<OportunidadeResposta>> AlterarVenda(
        Guid id,
        AlteracaoDaOportunidade alteracao,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        RepositorioDeContas contas,
        HttpContext http,
        CancellationToken cancelamento)
    {
        if (alteracao is { Titulo: null, ValorEstimado: null, PrevisaoDeFechamento: null, QuemDecide: null })
        {
            throw new PedidoInvalido("Diga o que muda na venda: titulo, valorEstimado, previsaoDeFechamento ou quemDecide.");
        }

        return Alterar(
            id,
            versao,
            repositorio,
            http,
            async venda =>
            {
                if (alteracao.Titulo is { } titulo)
                {
                    venda.Renomear(titulo);
                }

                if (alteracao.ValorEstimado is { } valor)
                {
                    venda.Estimar(Dinheiro.Reais(valor));
                }

                if (alteracao.PrevisaoDeFechamento is { } previsao)
                {
                    venda.PreverFechamento(previsao);
                }

                if (alteracao.QuemDecide is { } quemDecide)
                {
                    venda.DefinirQuemDecide((await Leituras.Conta(contas, venda.Conta.Valor, cancelamento)).Agregado, new ContatoId(quemDecide));
                }
            },
            cancelamento);
    }

    private static async Task<NoContent> Remover(Guid id, VersaoLida versao, RepositorioDeOportunidades repositorio, CancellationToken cancelamento)
    {
        await repositorio.Remover(new OportunidadeId(id), versao.Valor, cancelamento);

        return TypedResults.NoContent();
    }

    /// <summary>Passa a venda a outro vendedor — fechada inclusive: quem sai da empresa passa a carteira inteira.</summary>
    private static Task<Ok<OportunidadeResposta>> Transferir(
        Guid id,
        NovoResponsavel novo,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            venda =>
            {
                venda.TransferirPara(novo.Responsavel);

                return Task.CompletedTask;
            },
            cancelamento);

    private static Task<Ok<OportunidadeResposta>> Mover(
        Guid id,
        EtapaDeDestino destino,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            async venda => venda.MoverPara(await FunilDa(venda, funis, cancelamento), new EtapaId(destino.Etapa), fuso.Agora()),
            cancelamento);

    private static Task<Ok<OportunidadeResposta>> Ganhar(
        Guid id,
        Ganho ganho,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            async venda => venda.Ganhar(await FunilDa(venda, funis, cancelamento), Dinheiro.Reais(ganho.ValorFechado), fuso.Agora()),
            cancelamento);

    private static Task<Ok<OportunidadeResposta>> Perder(
        Guid id,
        PerdaDaVenda perda,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            async venda => venda.Perder(await FunilDa(venda, funis, cancelamento), perda.ParaPerda(), fuso.Agora()),
            cancelamento);

    private static Task<Ok<OportunidadeResposta>> Reabrir(
        Guid id,
        EtapaDeDestino destino,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        RepositorioDeFunis funis,
        Fuso fuso,
        HttpContext http,
        CancellationToken cancelamento) =>
        Alterar(
            id,
            versao,
            repositorio,
            http,
            async venda => venda.Reabrir(await FunilDa(venda, funis, cancelamento), new EtapaId(destino.Etapa), fuso.Agora()),
            cancelamento);

    /// <summary>O quadro do funil, agora: as vendas abertas em cada etapa, os totais e as paradas.</summary>
    private static async Task<Ok<QuadroResposta>> Quadro(
        Guid id,
        RepositorioDeFunis funis,
        RepositorioDeOportunidades repositorio,
        RepositorioDeContas contas,
        Fuso fuso,
        CancellationToken cancelamento)
    {
        var funil = (await Leituras.Funil(funis, id, cancelamento)).Agregado;
        var lidas = await repositorio.DoFunil(funil.Id, cancelamento);
        var versoes = lidas.ToDictionary(lida => lida.Agregado.Id, lida => lida.Versao);
        var nomes = (await contas.Listar(cancelamento)).ToDictionary(resumo => resumo.Id, resumo => resumo.Nome);
        var quadro = QuadroDoFunil.Do(funil, lidas.Select(lida => lida.Agregado), fuso.Agora());

        return TypedResults.Ok(new QuadroResposta(
            funil.Id.Valor,
            funil.Nome,
            quadro.Total.Valor,
            quadro.Ponderado.Valor,
            [
                .. quadro.Colunas.Select(coluna => new ColunaResposta(
                    coluna.Etapa.Id.Valor,
                    coluna.Etapa.Nome,
                    coluna.Etapa.Probabilidade,
                    coluna.Etapa.DiasEsperados,
                    coluna.Etapa.Arquivada,
                    coluna.Total.Valor,
                    coluna.Ponderado.Valor,
                    coluna.SemValor,
                    [
                        .. coluna.Oportunidades.Select(venda => new CartaoResposta(
                            venda.Id.Valor,
                            venda.Titulo,
                            venda.Conta.Valor,
                            nomes[venda.Conta],
                            venda.Responsavel,
                            venda.ValorEstimado?.Valor,
                            venda.PrevisaoDeFechamento,
                            venda.EntrouNaEtapaEm,
                            coluna.Paradas.Contains(venda),
                            versoes[venda.Id])),
                    ])),
            ]));
    }

    private static async Task<Created<OportunidadeResposta>> Gravar(
        Oportunidade venda,
        RepositorioDeOportunidades repositorio,
        HttpContext http,
        CancellationToken cancelamento)
    {
        Etiquetas.Escrever(http, await repositorio.Adicionar(venda, cancelamento));

        return TypedResults.Created($"/api/oportunidades/{venda.Id}", OportunidadeResposta.De(venda));
    }

    private static async Task<Ok<OportunidadeResposta>> Alterar(
        Guid id,
        VersaoLida versao,
        RepositorioDeOportunidades repositorio,
        HttpContext http,
        Func<Oportunidade, Task> alteracao,
        CancellationToken cancelamento)
    {
        var lida = await Leituras.Oportunidade(repositorio, id, cancelamento);
        Lidos.ConferirVersao(lida, versao, $"A oportunidade {id}");

        await alteracao(lida.Agregado);

        Etiquetas.Escrever(http, await repositorio.Atualizar(lida.Agregado, versao.Valor, cancelamento));

        return TypedResults.Ok(OportunidadeResposta.De(lida.Agregado));
    }

    private static async Task<Funil> FunilDa(Oportunidade venda, RepositorioDeFunis funis, CancellationToken cancelamento) =>
        (await Leituras.Funil(funis, venda.Funil.Valor, cancelamento)).Agregado;
}
