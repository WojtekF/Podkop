using Podkop.FindingComments.Application;
using Podkop.FindingComments.Infrastructure;
using Podkop.FindingComments.Server;
using Podkop.Findings.Infrastructure;
using Podkop.Findings.Server;
using Podkop.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
// Both slices seed from SampleSeed so the sample data stays coherent across them (issue #16).
builder.Services.AddFindings(() => SampleSeed.Findings);
builder.Services.AddFindingComments(() => SampleSeed.Comments);
builder.Services.AddSingleton<IFindingLookup, FindingsBackedFindingLookup>();
builder.Services.AddSingleton<ICurrentUser, StubCurrentUser>();

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
app.MapFindingComments();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;