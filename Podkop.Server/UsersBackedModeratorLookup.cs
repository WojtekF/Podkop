using Podkop.Moderation.Application;
using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Server;

/// <summary>
/// Composition-root adapter: answers the Moderation slice's <see cref="IModeratorLookup"/> port
/// from the Users slice's durable records, where roles live (issue #31). Slices never reference
/// each other's internals (ADR 0003) — only the host sees both sides, so the bridge lives here.
/// A user without a record holds no role and is no moderator.
/// </summary>
internal sealed class UsersBackedModeratorLookup(IUserRepository users) : IModeratorLookup
{
    public async Task<bool> IsModeratorAsync(string userName, CancellationToken cancellationToken)
    {
        var user = await users.GetByUserNameAsync(userName, cancellationToken);
        return user?.Role == UserRole.Moderator;
    }
}
