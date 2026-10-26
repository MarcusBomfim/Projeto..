using CrmObras.Observabilidade;
using CrmObras.Processamento;
using CrmObras.Processamento.Hospedagem;
using CrmObras.Processamento.Planejamento;

// Um host web só para as verificações de saúde: o trabalho é da fila, nos
// serviços em segundo plano.
var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration.GetConnectionString("Crm")
    ?? throw new InvalidOperationException("Falta a cadeia de conexão ConnectionStrings:Crm.");

builder.AdicionarObservabilidade("crm-obras-processador");
builder.Services.AdicionarProcessamento(conexao, planejamento => builder.Configuration.GetSection(OpcoesDoPlanejamento.Secao).Bind(planejamento));
builder.Services.AddHealthChecks()
    .VerificarBanco(conexao)
    .AddCheck<FilaGirando>("fila", tags: [Saude.Viva]);

var app = builder.Build();
app.MapearSaude();

await app.RunAsync();
