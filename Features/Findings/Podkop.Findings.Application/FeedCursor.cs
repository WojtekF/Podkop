using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Podkop.Findings.Application;

/// <summary>
///     Opaque cursor over the Main Page feed, encoding the position (id)
///     of the last item served. Clients must treat the string as a black box.
/// </summary>
public static class FeedCursor
{
    public static string Encode(Guid id)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString()));
    }

    public static bool TryDecode([NotNullWhen(false)] string? cursor, out Guid id)
    {
        void ApplyDefaultOutput(out Guid id)
        {
            id = Guid.Empty;
        }

        if (string.IsNullOrEmpty(cursor))
        {
            ApplyDefaultOutput(out id);
            return true;
        }

        try
        {
            var decodedBytes = Convert.FromBase64String(cursor);
            return Guid.TryParse(Encoding.UTF8.GetString(decodedBytes), out id);
        }
        catch (Exception ex)
        {
            throw new InvalidFeedCursorException(cursor, ex);
        }
    }
}