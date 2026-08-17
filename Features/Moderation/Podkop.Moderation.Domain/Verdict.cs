namespace Podkop.Moderation.Domain;

/// <summary>
///     A moderator's per-case ruling (CONTEXT.md, issue #35). Reports are immutable, so a
///     Verdict resolves them by reference: <see cref="ResolvedReportIds" /> captures exactly the
///     reports pending on the target at the instant the verdict was issued, and a report is
///     pending iff no Verdict references its id. A Case stays the derived grouping
///     (TargetKind + TargetId) of a target's pending reports — it exists iff at least one
///     report is pending — so a Verdict names the target and the resolved reports, never a case
///     id. The Verdict IS the Moderation Log entry: one entity, one store; later actions
///     (issue #36 removals, issue #39 bans) add their own records feeding the same log.
/// </summary>
public sealed class Verdict
{
    public Verdict(
        Guid id,
        string actor,
        ReportTargetKind targetKind,
        Guid targetId,
        VerdictKind kind,
        DateTimeOffset issuedAt,
        IReadOnlyList<Guid> resolvedReportIds)
    {
        Id = id;
        Actor = actor;
        TargetKind = targetKind;
        TargetId = targetId;
        Kind = kind;
        IssuedAt = issuedAt;
        ResolvedReportIds = resolvedReportIds;
    }

    public Guid Id { get; }

    /// <summary>The moderator who issued the ruling, by username.</summary>
    public string Actor { get; }

    public ReportTargetKind TargetKind { get; }
    public Guid TargetId { get; }
    public VerdictKind Kind { get; }
    public DateTimeOffset IssuedAt { get; }

    /// <summary>The reports this ruling resolved: every report pending on the target at <see cref="IssuedAt" />.</summary>
    public IReadOnlyList<Guid> ResolvedReportIds { get; }
}
