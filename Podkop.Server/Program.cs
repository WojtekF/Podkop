using Podkop.Findings.Infrastructure;
using Podkop.Findings.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddFindings();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.MapFindings();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;