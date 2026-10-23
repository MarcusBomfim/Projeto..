using CrmObras.Api.Hospedagem;

var builder = WebApplication.CreateBuilder(args);
builder.AdicionarApi();

var app = builder.Build();
app.UsarApi();

await app.RunAsync();
