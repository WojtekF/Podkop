using Podkop.Users.Domain;

namespace Podkop.Users.Application;

/// <summary>
///     Read access to the durable user records (issue #31). Usernames key records exactly —
///     no case folding, no normalization — the way authorship is compared everywhere.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
}
