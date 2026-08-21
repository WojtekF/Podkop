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
///     directly; that the shipped seed reaches the acting user through HTTP is pinned against
///     the database in <see cref="MyUserApiTests" />, since the worker owns seeding (issue #89).
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
}
