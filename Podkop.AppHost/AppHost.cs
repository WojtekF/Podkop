using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .WithPgWeb()
    .WithLifetime(ContainerLifetime.Persistent);

var podkopdb = postgres.AddDatabase("podkopdb");


var migrations = builder.AddProject<Podkop_MigrationService>("migrations")
    .WithReference(podkopdb)
    .WaitFor(podkopdb);


var server = builder
    .AddProject<Podkop_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithReference(podkopdb)
    .WaitForCompletion(migrations);


var webfrontend = builder
    .AddViteApp("webfrontend", "../frontend", "start")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
