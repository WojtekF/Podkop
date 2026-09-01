using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Infrastructure;
using Podkop.MigrationService;
using Podkop.Tags.Contracts;
using Podkop.Tags.Infrastructure;
using Podkop.Users.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Converted slices register their context and their SliceMigrationParticipant here as they move
// to PostgreSQL (ADR 0010's conversion order). Users converted first (issues #88/#89): the
// worker migrates and seeds the users schema, and the API host answers my-user from it.
builder.AddUsersPersistence();
builder.Services.AddSingleton(new SliceMigrationParticipant(
    "users",
    serviceProvider => serviceProvider.GetRequiredService<UsersDbContext>(),
    (serviceProvider, cancellationToken) => UsersSeed.SeedAsync(
        serviceProvider.GetRequiredService<UsersDbContext>(),
        SampleUsers.Generate(),
        cancellationToken)));

// Findings convert second (issue #67): the worker migrates and seeds the findings schema, and
// the API host answers the feed, the detail, and the votes from it.
builder.AddFindingsPersistence();
builder.Services.AddSingleton(new SliceMigrationParticipant(
    "findings",
    serviceProvider => serviceProvider.GetRequiredService<FindingsDbContext>(),
    (serviceProvider, cancellationToken) => FindingsSeed.SeedAsync(
        serviceProvider.GetRequiredService<FindingsDbContext>(),
        SampleFindings.Generate(),
        cancellationToken)));

// FindingComments convert third (issue #68), registered after Findings on purpose: the worker
// walks participants in registration order, and every seeded comment hangs off a finding the
// findings seed must already have put there. The comments generator regenerates the same finding
// ids the findings seed persisted — the deterministic-generation pact SampleSeed describes.
builder.AddFindingCommentsPersistence();
builder.Services.AddSingleton(new SliceMigrationParticipant(
    "finding_comments",
    serviceProvider => serviceProvider.GetRequiredService<FindingCommentsDbContext>(),
    (serviceProvider, cancellationToken) => FindingCommentsSeed.SeedAsync(
        serviceProvider.GetRequiredService<FindingCommentsDbContext>(),
        SampleFindingComments.GenerateFor([.. SampleFindings.Generate().Select(finding => finding.Id)]),
        cancellationToken)));

// Tags convert last (issue #77), registered after Findings on purpose: the worker walks
// participants in registration order, and every seeded membership row names a finding the
// findings seed must already have put there. The index is normally built only by consuming
// announce events — nothing announces a seeded finding — so the seed stands in for the
// announcements that never happened, projecting the same deterministic findings the findings
// seed persisted into the primitive announcement rows the Tags generator folds and files.
builder.AddTagsPersistence();
builder.Services.AddSingleton(new SliceMigrationParticipant(
    "tags",
    serviceProvider => serviceProvider.GetRequiredService<TagsDbContext>(),
    (serviceProvider, cancellationToken) => TagsSeed.SeedAsync(
        serviceProvider.GetRequiredService<TagsDbContext>(),
        SampleTagMemberships.GenerateFor(
        [
            .. SampleFindings.Generate().Select(finding => new SampleTaggedContent(
                TaggedContentTypes.Finding, finding.Id, finding.Tags, finding.CreatedAt))
        ]),
        cancellationToken)));

builder.Services.AddHostedService<MigrationWorker>();

// Test-only fault hook: the orchestration smoke suite sets this variable to prove that a
// failing migration keeps the API's startup gate closed. Never set it outside tests.
if (builder.Configuration["PODKOP_MIGRATIONS_FAULT"] is { Length: > 0 } faultMessage)
{
    builder.Services.AddSingleton(new SliceMigrationParticipant(
        "fault-injection",
        _ => throw new InvalidOperationException(faultMessage),
        (_, _) => Task.CompletedTask));
}

builder.Build().Run();
