using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.Users.Application;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The current-user endpoint (issue #31) through the HTTP seam: GET /api/my-user answers
///     the acting (stub) user's identity and role from the durable user records, the role
///     crossing the wire as the <c>UserRole</c> name. The records are overridden per spec so
///     both role spellings are pinned and the answer provably comes from the acting user's
///     record, not from the stub identity or any other record.
/// </summary>
public class MyUserApiTests
{
    private const string StubUser = "ada_lovelace";

    private static WebApplicationFactory<Program> CreateFactory(params User[] users) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IUserRepository>(new InMemoryUserRepository(users))));

    [Fact]
    public async Task My_user_answers_the_acting_users_identity_and_role()
    {
        // A moderator record for someone else sits alongside: the answer must be the acting
        // user's own Member record, so a handler answering any (or the "wrong") record fails.
        using var factory = CreateFactory(
            new User("grace_hopper", UserRole.Moderator),
            new User(StubUser, UserRole.Member));
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
        using var factory = CreateFactory(new User(StubUser, UserRole.Moderator));
        using var client = factory.CreateClient();

        var myUser = await client.GetFromJsonAsync<MyUserResponse>("/api/my-user");

        Assert.NotNull(myUser);
        Assert.Equal(StubUser, myUser.UserName);
        Assert.Equal("Moderator", myUser.Role);
    }

    private sealed record MyUserResponse(string UserName, string Role);
}
