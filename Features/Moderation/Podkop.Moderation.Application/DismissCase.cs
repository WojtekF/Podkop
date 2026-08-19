using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Command behind <c>POST /api/moderation/cases/{targetKind}/{targetId}/verdict</c> with a
///     <c>Dismissed</c> verdict (issue #35): the acting moderator rules the target's open Case
///     unfounded, resolving every report pending on it at once. Which verdict kinds exist on
///     the wire is the endpoint's vocabulary; this command is the Dismissed ruling — Upheld
///     arrives with issue #36 and its own consequences.
/// </summary>
public sealed record DismissCase(ReportTargetKind TargetKind, Guid TargetId) : IRequest<DismissCaseResult>;

public enum DismissCaseOutcome
{
    /// <summary>The case was dismissed — 204, no content.</summary>
    Dismissed,

    /// <summary>
    ///     The acting user holds no Moderator role — 403,
    ///     <c>podkop:problem:moderators-only</c>: members cannot issue verdicts.
    /// </summary>
    NotModerator,

    /// <summary>
    ///     The case is about the acting moderator's own content — 403,
    ///     <c>podkop:problem:own-case</c>: moderators never judge cases about their own content.
    /// </summary>
    OwnCase,

    /// <summary>
    ///     No open case exists for the target — 404, <c>podkop:problem:unknown-case</c>. A case
    ///     exists iff the target has at least one pending report, so a never-reported target
    ///     and an already-dismissed one answer identically.
    /// </summary>
    UnknownCase
}

/// <summary>The dismissal answer: how the command was ruled on.</summary>
public sealed record DismissCaseResult(DismissCaseOutcome Outcome);

/// <summary>
///     Dismisses a case (issue #35). The contract the specs pin down: the acting user must hold
///     the Moderator role, read through <see cref="IModeratorLookup" /> — anyone else is
///     <see cref="DismissCaseOutcome.NotModerator" />. A moderator must not dismiss a case
///     about their own content — the target's author, read through
///     <see cref="ICaseContentLookup" />, equals the acting username —
///     <see cref="DismissCaseOutcome.OwnCase" />; the never-against-another-moderator rule
///     deliberately does NOT constrain dismissals, or moderator-authored content could never be
///     cleared by anyone. The case is the target's pending reports — those whose ids no
///     Verdict's <see cref="Verdict.ResolvedReportIds" /> references; none pending means no
///     case, <see cref="DismissCaseOutcome.UnknownCase" />. Dismissing stores one
///     <see cref="Verdict" /> of kind <see cref="VerdictKind.Dismissed" /> capturing exactly
///     the pending report ids at dismiss time — reports themselves stay immutable — with the
///     actor from <see cref="ICurrentUser" /> and IssuedAt from the injected clock. Once
///     resolved, those reports stop counting everywhere pending-ness matters: the queue, the
///     duplicate rule, and the my-report state (their reporters may report the target afresh).
/// </summary>
public sealed class DismissCaseHandler(
    IReportRepository reportsRepository,
    IVerdictRepository verdictsRepository,
    ICurrentUser currentUser,
    IModeratorLookup moderatorLookup,
    ICaseContentLookup caseContentLookup,
    TimeProvider timeProvider)
    : IRequestHandler<DismissCase, DismissCaseResult>
{
    public async Task<DismissCaseResult> Handle(DismissCase request, CancellationToken cancellationToken)
    {
        if (!await moderatorLookup.IsModeratorAsync(currentUser.UserName, cancellationToken))
            return new DismissCaseResult(DismissCaseOutcome.NotModerator);

        var caseContent = await caseContentLookup.GetAsync(request.TargetKind, request.TargetId, cancellationToken);
        if (caseContent == null) return new DismissCaseResult(DismissCaseOutcome.UnknownCase);

        if (caseContent.Author == currentUser.UserName) return new DismissCaseResult(DismissCaseOutcome.OwnCase);

        var reports = await reportsRepository.GetByTargetAsync(request.TargetKind, request.TargetId, cancellationToken);

        var verdicts = await verdictsRepository.GetByTargetAsync(request.TargetKind,
            request.TargetId, cancellationToken);

        var reportsWithNoVerdict = reports
            .Where(r => r.IsPendingAgainst(verdicts))
            .Select(r => r.Id)
            .ToList();

        if (!reportsWithNoVerdict.Any()) return new DismissCaseResult(DismissCaseOutcome.UnknownCase);

        var verdict = new Verdict(
            Guid.CreateVersion7(),
            currentUser.UserName,
            request.TargetKind,
            request.TargetId,
            VerdictKind.Dismissed,
            timeProvider.GetUtcNow(),
            reportsWithNoVerdict.ToList());
        await verdictsRepository.AddAsync(verdict, cancellationToken);

        return new DismissCaseResult(DismissCaseOutcome.Dismissed);
    }
}
