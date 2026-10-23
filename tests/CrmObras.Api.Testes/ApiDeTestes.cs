using System.Collections.Concurrent;
using System.Net.Http.Headers;
using CrmObras.Api.Seguranca;
using CrmObras.Api.Testes;
using CrmObras.Infraestrutura.Testes.Persistencia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(ApiDeTestes))]

namespace CrmObras.Api.Testes;

/// <summary>
/// A API inteira, em memória, contra o PostgreSQL dos testes — sobe uma vez
/// para o assembly. Cada teste pede um cliente com o token que quiser: de que
/// construtora, com que papéis. A construtora de cada teste é nova, então os
/// testes não se enxergam, como duas construtoras não se enxergam.
/// </summary>
public sealed class ApiDeTestes : IAsyncLifetime
{
    /// <summary>O login de quem pede, quando o teste não diz outro.</summary>
    public const string LoginPadrao = "ana.vendas";

    private readonly BancoDeTestes _banco = new();

    private Fabrica? _fabrica;

    public BancoDeTestes Banco => _banco;

    public IServiceProvider Servicos => Api.Services;

    private Fabrica Api
    {
        get
        {
            Assert.SkipWhen(_fabrica is null, _banco.MotivoDaIndisponibilidade);

            return _fabrica!;
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _banco.InitializeAsync();

        if (_banco.Disponivel)
        {
            _fabrica = new Fabrica(_banco.ConexaoDaAplicacao);

            // Sobe a API aqui, uma vez. A fábrica sobe sozinha no primeiro
            // pedido — mas os testes começam em paralelo, e dois primeiros
            // pedidos subiriam duas APIs, cada uma com o seu relógio.
            _ = _fabrica.Services;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_fabrica is not null)
        {
            await _fabrica.DisposeAsync();
        }

        await _banco.DisposeAsync();
    }

    /// <summary>Um cliente com o token de alguém da construtora, com os papéis dados.</summary>
    public HttpClient Cliente(Guid construtora, params string[] papeis) => ClienteDe(construtora, LoginPadrao, papeis);

    /// <summary>Um cliente com o token de uma pessoa da construtora — o login é o responsável pelo que ela cria.</summary>
    public HttpClient ClienteDe(Guid construtora, string login, params string[] papeis) =>
        ClienteComToken(Tokens.De(construtora, login, papeis));

    public HttpClient ClienteComToken(string token)
    {
        var cliente = Anonimo();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    public HttpClient Anonimo() => Api.CreateClient();

    /// <summary>
    /// Para a construtora, o relógio da API passa a marcar este instante — e
    /// fica parado nele até o próximo acerto. As outras construtoras seguem
    /// no relógio do sistema: os testes rodam em paralelo.
    /// </summary>
    public void AcertarRelogio(Guid construtora, DateTimeOffset agora) =>
        Servicos.GetRequiredService<RelogioDosTestes>().Acertar(construtora, agora);

    /// <summary>A API configurada para os testes: o banco do contêiner, os tokens emitidos aqui e o relógio de cada construtora.</summary>
    private sealed class Fabrica(string conexao) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testes");
            builder.UseSetting("ConnectionStrings:Crm", conexao);
            builder.ConfigureTestServices(servicos =>
            {
                servicos.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opcoes =>
                {
                    opcoes.TokenValidationParameters.ValidIssuer = Tokens.Emissor;
                    opcoes.TokenValidationParameters.ValidAudience = Tokens.Audiencia;
                    opcoes.TokenValidationParameters.IssuerSigningKey = Tokens.Chave;
                });

                servicos.AddSingleton<RelogioDosTestes>();
                servicos.AddSingleton<TimeProvider>(provedor => provedor.GetRequiredService<RelogioDosTestes>());
            });
        }
    }
}

/// <summary>
/// Um relógio por construtora: a que o teste acertou marca o instante
/// acertado; as outras, o do sistema. A construtora é a do token do pedido em
/// curso.
/// </summary>
internal sealed class RelogioDosTestes(IHttpContextAccessor acessor) : TimeProvider
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _acertados = new();

    public void Acertar(Guid construtora, DateTimeOffset agora) => _acertados[construtora] = agora.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() =>
        ConstrutoraDoUsuario.Ler(acessor.HttpContext?.User) is { } construtora && _acertados.TryGetValue(construtora, out var agora)
            ? agora
            : base.GetUtcNow();
}
