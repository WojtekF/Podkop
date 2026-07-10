namespace Podkop.Findings.Application;

/// <summary>
/// Opaque cursor over the Main Page feed, encoding the position (promotion time + id)
/// of the last item served. Clients must treat the string as a black box.
/// </summary>
public static class FeedCursor
{
    public static string Encode(DateTimeOffset promotedAt, Guid id)
    {
        throw new NotImplementedException("Domain logic is implemented by the user (CLAUDE.md Feature Development Workflow).");
    }

    public static bool TryDecode(string cursor, out DateTimeOffset promotedAt, out Guid id)
    {
        throw new NotImplementedException("Domain logic is implemented by the user (CLAUDE.md Feature Development Workflow).");
    }
}
