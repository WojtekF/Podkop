using Podkop.MigrationService;
using Podkop.Users.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Converted slices register their context and their SliceMigrationParticipant here as they move
// to PostgreSQL (ADR 0010's conversion order). Users is the first (issue #88): the worker owns
// the only host that talks to the users schema until the endpoint conversion lands in #89.
builder.AddUsersPersistence();
builder.Services.AddSingleton(new SliceMigrationParticipant(
    "users",
    serviceProvider => serviceProvider.GetRequiredService<UsersDbContext>(),
    (serviceProvider, cancellationToken) => UsersSeed.SeedAsync(
        serviceProvider.GetRequiredService<UsersDbContext>(),
        SampleUsers.Generate(),
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
