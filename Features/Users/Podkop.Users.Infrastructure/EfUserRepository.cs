using Podkop.Users.Application;
using Podkop.Users.Domain;

namespace Podkop.Users.Infrastructure;

/// <summary>
///     The durable answer to <see cref="IUserRepository" /> (issue #89): user records live in
///     the slice's PostgreSQL schema, reached through <see cref="UsersDbContext" />. The lookup
///     answers the record whose username matches the asked-for name exactly — byte for byte,
///     the way authorship is compared everywhere (<c>voter == Author</c>) — and null when no
///     such record exists; a record differing only in case is not a match. The specs pin that
///     against the live database, where a lenient collation or a case-folding query would
///     betray the rule silently while every in-memory test kept passing.
/// </summary>
public sealed class EfUserRepository(UsersDbContext context) : IUserRepository
{
    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
