using Podkop.Users.Application;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The invariant edge of GetMyUser (issue #31): the seed guarantees the acting user a
///     record, so a lookup miss is a broken invariant and the handler throws
///     <see cref="InvalidOperationException" /> — never a quiet null, a 404, or silent
///     provisioning. Pinned at the handler seam because over HTTP every unhandled exception
///     is the same 500.
/// </summary>
public class GetMyUserHandlerTests
{
    private sealed class StubbedCurrentUser(string userName) : ICurrentUser
    {
        public string UserName => userName;
    }

    private static GetMyUserHandler HandlerFor(string actingUser, params User[] users) =>
        new(new InMemoryUserRepository(users), new StubbedCurrentUser(actingUser));

    [Fact]
    public async Task A_missing_record_for_the_acting_user_is_a_broken_invariant()
    {
        var handler = HandlerFor("ada_lovelace", new User("grace_hopper", UserRole.Moderator));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetMyUser(), CancellationToken.None));
    }

    [Fact]
    public async Task A_record_matching_only_by_case_is_not_the_acting_users()
    {
        // Usernames key records exactly — no case folding — the way authorship is compared
        // everywhere else (voter == Author).
        var handler = HandlerFor("ada_lovelace", new User("ADA_LOVELACE", UserRole.Member));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetMyUser(), CancellationToken.None));
    }
}
