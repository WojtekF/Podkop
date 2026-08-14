namespace Podkop.Users.Domain;

/// <summary>
///     The role a user acts with (issue #31): every user is a Member by default; a Moderator is
///     additionally empowered to judge Cases and apply moderation actions (CONTEXT.md). Roles
///     are assigned by seed only for now — there is no promotion flow.
/// </summary>
public enum UserRole
{
    Member,
    Moderator
}
