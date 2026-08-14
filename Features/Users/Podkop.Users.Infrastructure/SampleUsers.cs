using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

/// <summary>
///     Development seed for the user records until PostgreSQL persistence lands (issue #31):
///     one durable record per <see cref="Podkop.Shared.Infrastructure.SampleData.Authors" />
///     entry — the voter pool stays vote-keys only — with ada_lovelace and grace_hopper as the
///     Moderators (the stub acting user can reach the moderator area, and mod-vs-mod rules
///     have a live counterparty) and every other author a Member.
/// </summary>
public static class SampleUsers
{
    public static IReadOnlyList<User> Generate()
    {
        throw new NotImplementedException();
    }
}
