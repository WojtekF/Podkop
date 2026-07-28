using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Domain;
using Podkop.Findings.Infrastructure;

namespace Podkop.Server;

/// <summary>
///     Development seed for the whole app, coordinated here because only the composition root may
///     see every slice (ADR 0003). Each slice's Infrastructure owns its own generator
///     (<see cref="SampleFindings" />, <see cref="SampleFindingComments" />); this class only lines
///     their outputs up so the data is coherent: the seeded threads are the authority for comment
///     counts (issue #16) — every sample finding's <c>CommentCount</c> must equal the number of
///     seeded comments, replies included, attached to it. Generation is lazy so hosts and tests
///     that override the repositories never trigger it.
/// </summary>
internal static class SampleSeed
{
    private static readonly Lazy<(IReadOnlyList<Finding> Findings, IReadOnlyList<Comment> Comments)> Data =
        new(Generate);

    public static IReadOnlyList<Finding> Findings => Data.Value.Findings;
    public static IReadOnlyList<Comment> Comments => Data.Value.Comments;

    private static (IReadOnlyList<Finding> Findings, IReadOnlyList<Comment> Comments) Generate()
    {
        var findings = SampleFindings.Generate();
        var comments = SampleFindingComments.GenerateFor(findings.Select(f => f.Id).ToList());
        UpdateFindingsWithComments(findings, comments);
        return (findings, comments);
    }

    private static void UpdateFindingsWithComments(IEnumerable<Finding> findings, IReadOnlyList<Comment> comments)
    {
        foreach (var finding in findings) finding.UpdateCommentCount(comments.Count(c => c.FindingId == finding.Id));
    }
}