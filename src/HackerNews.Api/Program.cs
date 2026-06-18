using HackerNews.Api.Configuration;
using HackerNews.Application.Configuration;
using HackerNews.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCustomSerilog();
builder.Services.AddApiConfiguration(builder.Configuration);
builder.Services.ConfigureHealthChecks();
builder.Services.AddSwaggerConfiguration(builder.Environment);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();
app.UseApiConfiguration(app.Environment);
app.RunApplication();