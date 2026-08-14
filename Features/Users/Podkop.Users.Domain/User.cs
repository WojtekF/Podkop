namespace Podkop.Users.Domain;

/// <summary>
///     A durable user of the platform (issue #31), keyed by the same username content already
///     carries as Author and matched exactly, the way authorship is compared everywhere
///     (<c>voter == Author</c>). Holds nothing but identity and role; later tickets attach
///     their own state (bans, erasure) when they arrive.
/// </summary>
public sealed class User(string userName, UserRole role)
{
    public string UserName { get; } = userName;
    public UserRole Role { get; } = role;
}
