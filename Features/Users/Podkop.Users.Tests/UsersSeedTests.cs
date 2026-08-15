using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Podkop.Shared.Infrastructure;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The shipped user seed (issue #31): every author known to the platform holds a durable
///     record — the seed derives from the same author vocabulary the content seeds draw from,
///     so the invariant survives vocabulary edits — with exactly ada_lovelace (the stub acting
///     user, so the moderator area is reachable once role-gating lands) and grace_hopper (the
///     mod-vs-mod counterparty) as Moderators. The record facts are asserted on the generator
///     directly; the last spec runs the app as shipped, no overrides, through the same HTTP
///     surface the frontend uses.
/// </summary>
public class UsersSeedTests
{
    [Fact]
    public void Every_sample_author_holds_exactly_one_user_record()
    {
        var users = SampleUsers.Generate();

        Assert.Equal(
            SampleData.Authors.OrderBy(a => a, StringComparer.Ordinal),
            users.Select(u => u.UserName).OrderBy(a => a, StringComparer.Ordinal));
    }

    [Fact]
    public void Ada_and_grace_are_the_only_moderators()
    {
        var users = SampleUsers.Generate();

        Assert.Equal(
            ["ada_lovelace", "grace_hopper"],
            users.Where(u => u.Role == UserRole.Moderator)
                .Select(u => u.UserName)
                .OrderBy(a => a, StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_app_as_shipped_answers_the_stub_user_as_a_moderator()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var myUser = await client.GetFromJsonAsync<MyUserResponse>("/api/my-user");

        Assert.NotNull(myUser);
        Assert.Equal("ada_lovelace", myUser.UserName);
        Assert.Equal("Moderator", myUser.Role);
    }

    private sealed record MyUserResponse(string UserName, string Role);
}
