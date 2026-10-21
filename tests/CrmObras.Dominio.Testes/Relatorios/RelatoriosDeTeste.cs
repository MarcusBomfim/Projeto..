using CrmObras.Dominio.Clientes;
using CrmObras.Dominio.Comum;
using CrmObras.Dominio.Funis;
using CrmObras.Dominio.Oportunidades;

namespace CrmObras.Dominio.Testes.Relatorios;

/// <summary>
/// As vendas dos relatórios: num funil sem requisitos, para andar livre entre
/// as etapas, em instantes de 2026 no fuso de Brasília.
/// </summary>
internal static class RelatoriosDeTeste
{
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    public static readonly ContaId Cliente = new Conta("Logística Rota Sul").Id;

    /// <summary>Contato 10%, Visita 25%, Proposta 50%, Negociação 80%.</summary>
    public static Funil FunilSimples() =>
        new("Vendas", [new("Contato", 10), new("Visita", 25), new("Proposta", 50), new("Negociação", 80)]);

    public static DateTimeOffset Em(int dia, int mes, int hora = 9) => new(2026, mes, dia, hora, 0, 0, Brasilia);

    public static Oportunidade Venda(Funil funil, string titulo, DateTimeOffset criadaEm, decimal? valor = null, DateOnly? previsao = null)
    {
        var venda = new Oportunidade(Cliente, titulo, funil, "carlos.souza", criadaEm);

        if (valor is { } estimado)
        {
            venda.Estimar(Dinheiro.Reais(estimado));
        }

        if (previsao is { } data)
        {
            venda.PreverFechamento(data);
        }

        return venda;
    }

    public static Oportunidade Para(this Oportunidade venda, Funil funil, string etapa, DateTimeOffset quando)
    {
        venda.MoverPara(funil, funil.Abertas.Concat(funil.Arquivadas).Single(aberta => aberta.Nome == etapa).Id, quando);

        return venda;
    }

    public static Oportunidade Ganha(this Oportunidade venda, Funil funil, decimal valor, DateTimeOffset quando)
    {
        venda.Ganhar(funil, Dinheiro.Reais(valor), quando);

        return venda;
    }

    public static Oportunidade Perdida(this Oportunidade venda, Funil funil, DateTimeOffset quando, MotivoDaPerda motivo = MotivoDaPerda.Preco, string? concorrente = null)
    {
        venda.Perder(funil, new Perda(motivo, concorrente, motivo == MotivoDaPerda.Outro ? "Outro" : null), quando);

        return venda;
    }
}
