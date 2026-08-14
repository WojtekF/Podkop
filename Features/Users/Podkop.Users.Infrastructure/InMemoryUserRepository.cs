using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

/// <summary>In-memory user records until PostgreSQL persistence lands (issue #31).</summary>
public sealed class InMemoryUserRepository(IReadOnlyList<User> users) : IUserRepository
{
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(u => u.UserName == userName));
}
