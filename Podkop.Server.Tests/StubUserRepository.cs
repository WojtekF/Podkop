using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Server.Tests;

/// <summary>
///     The users store, doubled at the slice's own port: real user records live in PostgreSQL
///     since issue #89, so suites whose subject is the host's cross-slice wiring answer "who
///     holds which role" from a fixed list instead of hauling a database into specs that are
///     not about persistence. Exact-match semantics, the way the durable store answers.
/// </summary>
internal sealed class StubUserRepository(params User[] users) : IUserRepository
{
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(user => user.UserName == userName));
}
