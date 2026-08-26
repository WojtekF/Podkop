using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Shared.Testing;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The current-user endpoint over the durable store (issues #31, #89): GET /api/my-user
///     answers the acting (stub) user's identity and role from PostgreSQL, the role crossing
///     the wire as the <c>UserRole</c> name. The specs put records into the real database and
///     override no service, so whatever repository the production wiring resolves is what
///     answers — pinning both role spellings, the answer provably coming from the acting
///     user's own record, and the shipped seed pact now that the worker owns seeding.
/// </summary>
[Collection(UsersDatabaseCollection.Name)]
public class MyUserApiTests(UsersPostgresDatabase database) : IAsyncLifetime
{
    private const string StubUser = "ada_lovelace";

    // Every spec starts from an empty, fully migrated database. Each one inserts the stub
    // user's record, so a reset that quietly stopped working surfaces as a key collision in
    // the arrangement rather than as a false pass.
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithPodkopDatabase(database.ConnectionString);

    private async Task GivenUserRecords(params User[] users)
    {
        await using var context = database.CreateDbContext();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task My_user_answers_the_acting_users_identity_and_role_from_the_database()
    {
        // A moderator record for someone else sits alongside: the answer must be the acting
        // user's own Member record, so a handler answering any (or the "wrong") record fails.
        // The Member role also polices the host: ada_lovelace ships as a Moderator in the
        // sample seed, so a host that still seeded sample users on its own would collide with
        // this record before the spec even asks.
        await GivenUserRecords(
            new User("grace_hopper", UserRole.Moderator),
            new User(StubUser, UserRole.Member));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/my-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var myUser = await response.Content.ReadFromJsonAsync<MyUserResponse>();
        Assert.NotNull(myUser);
        Assert.Equal(StubUser, myUser.UserName);
        Assert.Equal("Member", myUser.Role);
    }

    [Fact]
    public async Task A_moderator_record_crosses_the_wire_as_the_role_name()
    {
        await GivenUserRecords(new User(StubUser, UserRole.Moderator));
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var myUser = await client.GetFromJsonAsync<MyUserResponse>("/api/my-user");

        Assert.NotNull(myUser);
        Assert.Equal(StubUser, myUser.UserName);
        Assert.Equal("Moderator", myUser.Role);
    }

    [Fact]
    public async Task The_sample_seeded_database_answers_the_stub_user_as_a_moderator()
    {
        // The worker's own machinery populates the database here — the same seed a fresh
        // orchestrated database receives — so the shipped pact (issue #31) holds end to end:
        // the stub acting user is among the sample users and holds the Moderator role.
        await using (var context = database.CreateDbContext())
        {
            await UsersSeed.SeedAsync(context, SampleUsers.Generate(), CancellationToken.None);
        }

        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var myUser = await client.GetFromJsonAsync<MyUserResponse>("/api/my-user");

        Assert.NotNull(myUser);
        Assert.Equal(StubUser, myUser.UserName);
        Assert.Equal("Moderator", myUser.Role);
    }

    private sealed record MyUserResponse(string UserName, string Role);
}
