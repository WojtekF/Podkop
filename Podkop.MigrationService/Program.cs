using Podkop.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

// Converted slices register their SliceMigrationParticipant here as they move to PostgreSQL
// (ADR 0010's conversion order). None has yet (issue #87), so the worker's registry resolves
// empty and both of its steps must complete trivially.
builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
