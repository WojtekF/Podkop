using MediatR;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     Query behind <c>GET /api/moderation/log</c> (issue #35): the Moderation Log — the
///     internal record of every moderation action (CONTEXT.md). The Verdict IS the log entry:
///     one entity, one store; every seeded or issued verdict lists here, and later actions
///     (issue #36 removals, issue #39 bans) add their own records feeding the same endpoint.
///     A flat, unpaginated list this ticket.
/// </summary>
public sealed record GetModerationLog : IRequest<ModerationLogResult>;

/// <summary>
///     How the log query answered: the log was listed, or the acting user holds no moderator
///     role and is refused — the log is a moderators-only surface (CONTEXT.md).
/// </summary>
public enum ModerationLogOutcome
{
    Listed,
    NotModerator
}

/// <summary>The log answer: the outcome, and the ordered entries when it was listed.</summary>
public sealed record ModerationLogResult(ModerationLogOutcome Outcome, IReadOnlyList<ModerationLogEntry>? Entries);

/// <summary>
///     One Moderation Log entry as the log shows it (issue #35): who acted, on what, how, and
///     when. TargetKind carries the <c>ReportTargetKind</c> name ("Finding" / "Comment") and
///     Verdict the <c>VerdictKind</c> name ("Dismissed") across the wire;
///     ResolvedReportCount is how many pending reports the ruling resolved. Reporter
///     identities never leave the slice — the log names the acting moderator, never who
///     reported.
/// </summary>
public sealed record ModerationLogEntry(
    string Actor,
    string TargetKind,
    Guid TargetId,
    string Verdict,
    DateTimeOffset IssuedAt,
    int ResolvedReportCount);

/// <summary>
///     Answers the Moderation Log (issue #35). The contract the specs pin down: the acting
///     user must hold the Moderator role, read through <see cref="IModeratorLookup" /> —
///     anyone else is refused with <see cref="ModerationLogOutcome.NotModerator" />. Every
///     stored verdict is an entry, ordered newest first by IssuedAt — the store promises no
///     order, so ordering is this handler's job. An empty store lists an empty log.
/// </summary>
public sealed class GetModerationLogHandler(
    IVerdictRepository verdictsRepository,
    ICurrentUser currentUser,
    IModeratorLookup moderatorLookup)
    : IRequestHandler<GetModerationLog, ModerationLogResult>
{
    public Task<ModerationLogResult> Handle(GetModerationLog request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
