using CrmObras.Api.Recursos.Contas;
using CrmObras.Api.Recursos.Funis;
using CrmObras.Api.Recursos.Oportunidades;
using CrmObras.Api.Seguranca;

namespace CrmObras.Api.Testes;

/// <summary>
/// Uma construtora só do teste, com o comercial dela — o Carlos vende, a
/// Helena é gerente, o Roberto é diretor —, o relógio dela e os passos que
/// toda venda pela API começa dando: a conta da filial, o funil padrão, a
/// venda.
/// </summary>
internal sealed class CenarioDeVenda(ApiDeTestes api)
{
    /// <summary>A segunda-feira de 19/10/2026, às 9h em Brasília: onde o exemplo do README começa.</summary>
    public static readonly DateTimeOffset Segunda = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(-3));

    public Guid Construtora { get; } = Guid.CreateVersion7();

    public HttpClient Carlos => api.ClienteDe(Construtora, "carlos.souza", Papeis.Vendedor);

    public HttpClient Helena => api.ClienteDe(Construtora, "helena.martins", Papeis.Gerente);

    public HttpClient Roberto => api.ClienteDe(Construtora, "roberto.alves", Papeis.Diretor);

    /// <summary>Alguém da construtora sem papel nenhum: lê, e só.</summary>
    public HttpClient Leitor => api.ClienteDe(Construtora, "estagiario", []);

    /// <summary>Um instante de 2026, em Brasília.</summary>
    public static DateTimeOffset Em(int dia, int mes, int hora, int minuto = 0) => new(2026, mes, dia, hora, minuto, 0, Segunda.Offset);

    /// <summary>Daqui em diante, a API marca este instante para a construtora.</summary>
    public void Relogio(DateTimeOffset agora) => api.AcertarRelogio(Construtora, agora);

    /// <summary>A filial da Rota Sul, com a Marina e o Sérgio, que decide.</summary>
    public async Task<(Guid Conta, Guid Marina, Guid Sergio)> Filial()
    {
        var filial = await Carlos.Criar("/api/contas", new
        {
            nome = "Rota Sul Logística — Campinas",
            documento = Exemplos.CnpjDaFilial,
            contatos = new object[]
            {
                new { nome = "Marina Costa", email = "marina.costa@rotasul.com.br" },
                new { nome = "Sérgio Lima", email = "sergio.lima@rotasul.com.br", papel = "Decisor" },
            },
        }).LerComSucesso<ContaResposta>();

        return (filial.Id, filial.Contatos[0].Id, filial.Contatos[1].Id);
    }

    /// <summary>O funil padrão da construtora, criado pela gerente.</summary>
    public Task<FunilResposta> FunilPadrao() => Helena.Criar("/api/funis/padrao", null).LerComSucesso<FunilResposta>();

    /// <summary>Uma venda do Carlos, na primeira etapa.</summary>
    public Task<OportunidadeResposta> Venda(Guid conta, FunilResposta funil, string titulo = "Galpão de 1.200 m² em Jundiaí") =>
        Carlos.Criar("/api/oportunidades", new { conta, funil = funil.Id, titulo }).LerComSucesso<OportunidadeResposta>();
}
