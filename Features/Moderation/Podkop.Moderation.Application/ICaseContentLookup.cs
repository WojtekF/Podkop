using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     The facts the case queue needs about reported content, whichever slice owns it
///     (issue #34): who authored it, the text its preview is cut from, and the finding page
///     where it lives. <c>null</c> when no content of that kind has that id — for stored
///     reports a broken invariant today, since nothing removes content yet. Distinct from
///     <see cref="IReportTargetLookup" />, which answers the filing flow's narrower question;
///     features never reference each other's internals (ADR 0003), so the composition root
///     implements this port over the Findings and FindingComments slices, dispatching on the
///     target kind.
/// </summary>
public interface ICaseContentLookup
{
    Task<CaseContent?> GetAsync(ReportTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);
}

/// <summary>
///     A piece of reported content as the queue sees it: its author, its preview source text
///     — a finding's title, a comment's full text; cutting to the preview cap is the queue's
///     rule, not this port's — and the finding whose page shows it (a finding: itself; a
///     comment: the finding it belongs to).
/// </summary>
public sealed record CaseContent(string Author, string Preview, Guid FindingId);
