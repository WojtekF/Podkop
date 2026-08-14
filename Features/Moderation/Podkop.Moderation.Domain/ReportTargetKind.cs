namespace Podkop.Moderation.Domain;

/// <summary>
///     The kind of content a report targets (issue #33). A Report cites its target by kind + id
///     because findings and comments live in different slices yet share every reporting rule —
///     one report per user per target, no self-reports, pinned Statute version. Issue #34 groups
///     Cases over both kinds.
/// </summary>
public enum ReportTargetKind
{
    Finding,
    Comment
}
