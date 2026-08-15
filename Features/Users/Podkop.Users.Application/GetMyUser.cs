using MediatR;

namespace Podkop.Users.Application;

/// <summary>
///     Query behind <c>GET /api/my-user</c> (issue #31): who the acting user is and what role
///     they hold, answered from the durable user records through the slice's own current-user
///     port. The seed guarantees a record for every author, the stub user included, so a
///     missing record is a broken invariant — the handler throws (surfacing as 500) rather
///     than answering 404 or provisioning silently; when real auth replaces the stub,
///     provisioning becomes the auth layer's job, not this query's.
/// </summary>
public sealed record GetMyUser : IRequest<MyUserDetail>;

/// <summary>
///     The acting user's identity and role; Role carries the <c>UserRole</c> name
///     ("Member" / "Moderator") across the wire.
/// </summary>
public sealed record MyUserDetail(string UserName, string Role);

public sealed class GetMyUserHandler(IUserRepository userRepository, ICurrentUser currentUser)
    : IRequestHandler<GetMyUser, MyUserDetail>
{
    public async Task<MyUserDetail> Handle(GetMyUser request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUserNameAsync(currentUser.UserName, cancellationToken);

        if (user is null) throw new InvalidOperationException($"User {currentUser.UserName} not found");

        return new MyUserDetail(user.UserName, user.Role.ToString());
    }
}
