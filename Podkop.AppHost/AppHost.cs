var builder = DistributedApplication.CreateBuilder(args);

// Postgres orchestration foundation (issue #87, ADR 0010) — the graph this file must grow,
// pinned by the smoke suite in Podkop.AppHost.Tests:
//   - a PostgreSQL server resource named "postgres", its data kept on a data volume and its
//     container kept alive across app runs (persistent lifetime), with pgAdmin alongside for
//     inspection, exposing a database named "podkopdb";
//   - a resource named "migrations" running the Podkop.MigrationService worker against that
//     database, started only once the database is ready;
//   - the existing "server" resource referencing "podkopdb" and held back until "migrations"
//     has run to completion — the API must never serve before the worker finishes.

var server = builder.AddProject<Projects.Podkop_Server>("server")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend", "start")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();