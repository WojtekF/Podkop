using Podkop.FindingComments.Application;
using Podkop.FindingComments.Contracts;
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
using Podkop.Shared.Infrastructure.Outbox;
using Podkop.Tags.Contracts;
using Podkop.Tags.Infrastructure;
using Podkop.Tags.Server;
using Podkop.Users.Infrastructure;
using Podkop.Users.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
// Findings answer from PostgreSQL (issue #67): the slice takes no seed — sample findings reach
// the database only through the migration worker — and the API host registers the slice's
// context against the orchestrated podkopdb connection.
builder.Services.AddFindings();
builder.AddFindingsPersistence();
// Comments answer from PostgreSQL too (issue #68): the slice takes no seed — sample discussions
// reach the database only through the migration worker — and the API host registers the slice's
// context against the orchestrated podkopdb connection.
builder.Services.AddFindingComments();
builder.AddFindingCommentsPersistence();
builder.Services.AddDocuments(() => SampleSeed.StatuteVersions, () => SampleSeed.PrivacyPolicyVersions);
// Reports seed too since the case queue made them observable (issue #34), and verdicts since
// the Moderation Log made them observable (issue #35).
builder.Services.AddModeration(() => SampleSeed.Reports, () => SampleSeed.Verdicts);
// Users answer from PostgreSQL (issue #89): the slice takes no seed — sample users reach the
// database only through the migration worker — and the API host registers the slice's context
// against the orchestrated podkopdb connection.
builder.Services.AddUsers();
builder.AddUsersPersistence();
// The tag namespace answers from PostgreSQL too (issue #77): the slice takes no seed — the
// sample membership index reaches the database only through the migration worker — and the API
// host registers the slice's context so it can answer tag pages and index the announcements the
// outbox delivers to it.
builder.Services.AddTags();
builder.AddTagsPersistence();
// Scoped to match the EF-backed IFindingRepository they read through (issue #67).
builder.Services.AddScoped<IFindingLookup, FindingsBackedFindingLookup>();
builder.Services.AddScoped<IReportTargetLookup, ContentBackedReportTargetLookup>();
builder.Services.AddScoped<ICaseContentLookup, ContentBackedCaseContentLookup>();
// Scoped to match the EF-backed IUserRepository it reads through (issue #89).
builder.Services.AddScoped<IModeratorLookup, UsersBackedModeratorLookup>();
builder.Services.AddScoped<IFindingCommentsLookup, CommentsBackedFindingCommentsLookup>();
// Scoped to match the ISender it dispatches the Documents slice's current-statute query through.
builder.Services.AddScoped<IStatuteLookup, DocumentsBackedStatuteLookup>();
builder.Services.AddSingleton(TimeProvider.System);

// Every slice owns its own current-user port (ADR 0003); the same stub backs them all
// (issues #13, #15, #32). Qualified because the port name repeats across the slices.
builder.Services.AddSingleton<Podkop.FindingComments.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Findings.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Moderation.Application.ICurrentUser, StubCurrentUser>();
builder.Services.AddSingleton<Podkop.Users.Application.ICurrentUser, StubCurrentUser>();

// Outbox/inbox processing.
builder.Services.AddSingleton<OutboxProcessorOptions>();
builder.Services.AddSingleton<ContractEventTypeRegistry>(provider =>
    new ContractEventTypeRegistry([
        typeof(CommentPosted),
        // The tag namespace's announce pair (issue #77): Findings writes them, Tags consumes
        // them, and only this composition root sees both Contracts projects.
        typeof(TaggedContentAnnounced),
        typeof(TaggedContentRemoved),
    ]));
builder.Services.AddScoped<IContractEventPublisher, MediatRBackedContractEventPublisher>();
builder.Services.AddHostedService<OutboxProcessingService>();

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
app.MapTags();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();

// Exposes the entry point to WebApplicationFactory-based integration tests.
public partial class Program;
