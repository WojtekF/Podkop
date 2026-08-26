namespace Podkop.Shared.Infrastructure;

/// <summary>
///     The plan a sample finding's discussion follows — shared because, since issue #67, the two
///     generators that must agree on it run in different processes: the migration worker writes
///     the findings, comment counts included, to PostgreSQL, while the API host still seeds the
///     discussions in memory, and no coordinator sees both sides to line them up after the fact.
///     They tell one story only if everything the story depends on is a pure function of the
///     finding's identity, so this class answers, for a finding id, how many comments the
///     discussion holds (replies included) and hands out the stream the discussion's remaining
///     choices draw from. Like <see cref="SampleData" />, it holds no domain types and makes no
///     content choices of its own — the slices keep owning their generators (ADR 0003); this is
///     only the arithmetic those generators must share to agree.
/// </summary>
public static class SampleDiscussions
{
    /// <summary>The most comments, replies included, a sample discussion carries.</summary>
    public const int MaxCommentCount = 24;

    // Distinct salts keep the two derivations independent: the content stream's first draw must
    // not merely repeat the count.
    private const int CountStream = 0x00C0FFEE;
    private const int ContentStream = 0x0BADF00D;

    /// <summary>
    ///     How many comments the finding's discussion holds, replies included. The findings
    ///     generator stamps this on the finding that gets persisted; the comments generator emits
    ///     exactly this many — that equality is the comment-count pact (issue #16) carried across
    ///     the persistence boundary.
    /// </summary>
    public static int CommentCountFor(Guid findingId) =>
        new Random(SeedFor(findingId, CountStream)).Next(0, MaxCommentCount + 1);

    /// <summary>
    ///     The stream every other choice in the finding's discussion is drawn from. Seeded per
    ///     finding on purpose: a draw added to one discussion must never reshuffle the others.
    /// </summary>
    public static Random RandomFor(Guid findingId) => new(SeedFor(findingId, ContentStream));

    // Folds all sixteen bytes so any id scheme yields distinct seeds — the sample finding ids
    // vary only in their trailing bytes. Spelled out by hand because HashCode.Combine is salted
    // anew each process start, and both processes must derive the same seed from the same id.
    private static int SeedFor(Guid findingId, int stream)
    {
        var bytes = findingId.ToByteArray();
        var seed = stream;
        for (var offset = 0; offset < bytes.Length; offset += 4)
            seed = unchecked(seed * 397 ^ BitConverter.ToInt32(bytes, offset));
        return seed;
    }
}
