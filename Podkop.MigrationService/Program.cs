using Podkop.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

// Converted slices register their SliceMigrationParticipant here as they move to PostgreSQL
// (ADR 0010's conversion order). None has yet (issue #87), so the worker's registry resolves
// empty and both of its steps must complete trivially.
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
