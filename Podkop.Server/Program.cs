using Podkop.FindingComments.Application;
using Podkop.FindingComments.Infrastructure;
using Podkop.FindingComments.Server;
using Podkop.Findings.Infrastructure;
using Podkop.Findings.Server;
using Podkop.Moderation.Application;
using Podkop.Moderation.Infrastructure;
using Podkop.Moderation.Server;
using Podkop.Server;
using Podkop.Documents.Infrastructure;
using Podkop.Documents.Server;
using Podkop.Users.Infrastructure;
using Podkop.Users.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
// Both slices seed from SampleSeed so the sample data stays coherent across them (issue #16).
builder.Services.AddFindings(() => SampleSeed.Findings);
builder.Services.AddFindingComments(() => SampleSeed.Comments);
builder.Services.AddDocuments(() => SampleSeed.StatuteVersions, () => SampleSeed.PrivacyPolicyVersions);
// Reports are member-created and never seeded (issue #32), so the Moderation slice takes no seed.
builder.Services.AddModeration();
builder.Services.AddUsers(() => SampleSeed.Users);
builder.Services.AddSingleton<IFindingLookup, FindingsBackedFindingLookup>();
builder.Services.AddSingleton<IReportTargetLookup, ContentBackedReportTargetLookup>();
builder.Services.AddSingleton<IFindingCommentsLookup, CommentsBackedFindingCommentsLookup>();
// Scoped to match the ISender it dispatches the Documents slice's current-statute query through.
builder.Services.AddScoped<IStatuteLookup, DocumentsBackedStatuteLookup>();
builder.Services.AddSingleton(TimeProvider.System);

// Every slice owns its own current-user port (ADR 0003); the same stub backs them all
// (issues #13, #15, #32). Qualified because the port name repeats across the slices.
builder.Services.AddSingleton<Podkop.FindingComments.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Findings.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Moderation.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Users.Application.ICurrentUser, StubCurrentUser>();

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
app.MapDocuments();
app.MapModeration();
app.MapUsers();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;
