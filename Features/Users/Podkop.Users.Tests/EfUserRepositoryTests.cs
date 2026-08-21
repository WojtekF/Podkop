using Podkop.Users.Application;
using Podkop.Users.Domain;
using Podkop.Users.Infrastructure;

namespace Podkop.Users.Tests;

/// <summary>
///     The EF-backed repository against the live database (issue #89): the same
///     <see cref="IUserRepository" /> contract the handler specs pin with a double, now proven
///     where it can actually break — the lookup answers the record whose username matches
///     exactly, answers null for an unknown name, and never case-folds. The case spec runs
///     against real PostgreSQL on purpose: a lenient collation or a case-folding query would
///     betray the exact-match rule here while every in-memory test kept passing.
/// </summary>
[Collection(UsersDatabaseCollection.Name)]
public class EfUserRepositoryTests(UsersPostgresDatabase database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task GivenUserRecords(params User[] users)
    {
        await using var context = new UsersDbContextFactory().CreateDbContext([database.ConnectionString]);
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task<User?> LookedUp(string userName)
    {
        await using var context = new UsersDbContextFactory().CreateDbContext([database.ConnectionString]);
        return await new EfUserRepository(context).GetByUserNameAsync(userName, CancellationToken.None);
    }

    [Fact]
    public async Task Answers_the_record_matching_the_username_exactly()
    {
        // A decoy with the other role sits alongside, so answering any record fails.
        await GivenUserRecords(
            new User("grace_hopper", UserRole.Moderator),
            new User("ada_lovelace", UserRole.Member));

        var user = await LookedUp("ada_lovelace");

        Assert.NotNull(user);
        Assert.Equal("ada_lovelace", user.UserName);
        Assert.Equal(UserRole.Member, user.Role);
    }

    [Fact]
    public async Task Answers_null_when_no_record_carries_the_username()
    {
        await GivenUserRecords(new User("grace_hopper", UserRole.Moderator));

        Assert.Null(await LookedUp("ada_lovelace"));
    }

    [Fact]
    public async Task A_record_differing_only_in_case_is_not_a_match()
    {
        // Usernames key records exactly — no case folding — the way authorship is compared
        // everywhere else (voter == Author).
        await GivenUserRecords(new User("ADA_LOVELACE", UserRole.Member));

        Assert.Null(await LookedUp("ada_lovelace"));
    }
}
