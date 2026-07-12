using System.Text;

namespace Podkop.Findings.Application;

/// <summary>
///     Opaque cursor over the Main Page feed, encoding the position (promotion time + id)
///     of the last item served. Clients must treat the string as a black box.
/// </summary>
public static class FeedCursor
{
    public static string Encode(DateTimeOffset promotedAt, Guid id)
    {
        var toEncode = $"{promotedAt.ToUnixTimeSeconds()}|{id}";

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(toEncode));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset promotedAt, out Guid id)
    {
        void ApplyDefaultOutput(out DateTimeOffset promotedAt, out Guid id)
        {
            promotedAt = default;
            id = Guid.Empty;
        }

        if (string.IsNullOrEmpty(cursor))
        {
            ApplyDefaultOutput(out promotedAt, out id);
            return true;
        }

        try
        {
            var decodedBytes = Convert.FromBase64String(cursor);
            var decoded = Encoding.UTF8.GetString(decodedBytes);
            var split = decoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                ApplyDefaultOutput(out promotedAt, out id);
                return false;
            }

            promotedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(split[0]));
            id = Guid.Parse(split[1]);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidFeedCursorException(cursor, ex);
        }
    }
}