using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

/// <summary>
///     Development seed for the dismissed verdicts until PostgreSQL persistence lands
///     (issue #35), so the Moderation Log has entries — and the case queue a resolved history —
///     without hand-dismissing cases first. The generated verdicts must be coherent with the
///     given reports — every one could have been issued: its actor is one of the given
///     moderators and never the judged target's own author (moderators never judge cases about
///     their own content), its kind is Dismissed (Upheld arrives with issue #36), and its
///     ResolvedReportIds capture exactly the given reports of its target filed before its
///     IssuedAt — a dismissal resolves the whole pending case, never part of one — so every
///     resolved id references a real seeded report. Each target is judged at most once in the
///     seed. Coverage the log and queue make observable: at least two dismissals; IssuedAt
///     instants distinct — so the newest-first log order is stable and visible — each after
///     every report it resolves; and at least one judged target is re-reported afterwards —
///     its remaining given reports all filed after the dismissal (the report seed's fresh
///     wave) — so the shipped queue carries a fresh case whose older sibling reports are
///     resolved.
/// </summary>
public static class SampleVerdicts
{
    public static IReadOnlyList<Verdict> GenerateFor(
        IReadOnlyList<SampleReportTarget> targets,
        IReadOnlyList<Report> reports,
        IReadOnlyList<string> moderators) =>
        throw new NotImplementedException();
}
