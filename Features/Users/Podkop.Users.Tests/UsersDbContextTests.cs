using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The Users slice's persistence shape (issue #88, ADR 0010), read off the model a host
///     actually builds: schema, identifier spelling, key, how the role reaches the database, and
///     where applied migrations get recorded. Nothing here opens a connection — a built model and
///     the context's relational options answer every one of these — so the suite needs neither
///     Docker nor orchestration; the physical facts are proven against real PostgreSQL by the
///     orchestration suite in <c>Podkop.AppHost.Tests</c>.
/// </summary>
public class UsersDbContextTests : IDisposable
{
    // Registration must not connect, so any well-formed connection string will do here. It is
    // deliberately not the orchestrated one: a test that only builds a model must never be able
    // to reach a real database by accident.
    private const string UnreachableConnectionString =
        "Host=localhost;Port=1;Database=podkopdb;Username=podkop;Password=podkop";

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--) _disposables[i].Dispose();
    }

    /// <summary>The context exactly as a host that called the slice's persistence entry point gets it.</summary>
    private UsersDbContext RegisteredContext()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:podkopdb"] = UnreachableConnectionString
        });

        builder.AddUsersPersistence();

        var host = builder.Build();
        _disposables.Add(host);
        var scope = host.Services.CreateScope();
        _disposables.Add(scope);
        return scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    }

    [Fact]
    public void The_user_records_live_in_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var entityType = context.Model.FindEntityType(typeof(User));

        Assert.NotNull(entityType);
        Assert.Equal("users", context.Model.GetDefaultSchema());
        Assert.Equal("users", entityType.GetSchema());
    }

    [Fact]
    public void Tables_and_columns_are_spelled_the_way_the_database_spells_identifiers()
    {
        using var context = RegisteredContext();
        var entityType = context.Model.FindEntityType(typeof(User))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        Assert.Equal("users", entityType.GetTableName());
        Assert.Equal("user_name", entityType.FindProperty(nameof(User.UserName))!.GetColumnName(table));
        Assert.Equal("role", entityType.FindProperty(nameof(User.Role))!.GetColumnName(table));
    }

    [Fact]
    public void A_user_record_is_keyed_by_its_username()
    {
        using var context = RegisteredContext();

        var key = context.Model.FindEntityType(typeof(User))!.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal([nameof(User.UserName)], key.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void The_role_reaches_the_database_as_its_name()
    {
        using var context = RegisteredContext();
        var role = context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.Role))!;

        var storedType = role.GetProviderClrType() ?? role.GetValueConverter()?.ProviderClrType;

        Assert.True(
            storedType == typeof(string),
            "The role must be stored as readable text so values stay legible in psql and survive "
            + "a reordering of the enum (ADR 0010), but the model stores it as "
            + $"{storedType?.Name ?? "the enum's underlying number"}.");
    }

    [Fact]
    public void Applied_migrations_are_recorded_inside_the_slices_own_schema()
    {
        using var context = RegisteredContext();

        var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal("users", relational.MigrationsHistoryTableSchema);
        Assert.Equal("__EFMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Equal(typeof(UsersDbContext).Assembly.GetName().Name, relational.MigrationsAssembly);
    }
}
