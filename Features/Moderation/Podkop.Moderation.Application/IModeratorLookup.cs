namespace Podkop.Moderation.Application;

/// <summary>
///     The one fact this slice needs about a user's standing: whether they hold the Moderator
///     role (CONTEXT.md) — the gate on the case queue and, from issue #35 on, every moderation
///     action. Roles are the Users slice's truth; features never reference each other's
///     internals (ADR 0003), so the composition root implements this port over the Users
///     slice. A user it does not know is not a moderator.
/// </summary>
public interface IModeratorLookup
{
    Task<bool> IsModeratorAsync(string userName, CancellationToken cancellationToken);
}
