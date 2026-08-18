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
        IReadOnlyList<string> moderators)
    {
        var judgeable = reports
            .GroupBy(report => (report.TargetKind, report.TargetId))
            .Select(byTarget => (
                Target: targets.Single(target =>
                    target.Kind == byTarget.Key.TargetKind && target.Id == byTarget.Key.TargetId),
                Reports: byTarget.OrderBy(report => report.FiledAt).ToList()))
            .Where(candidate => moderators.Any(moderator => moderator != candidate.Target.Author))
            .ToList();
        if (judgeable.Count == 0) return [];

        var verdicts = new List<Verdict>();

        Verdict Dismissal(SampleReportTarget target, List<Report> given, DateTimeOffset issuedAt) => new(
            Guid.CreateVersion7(),
            moderators.Where(moderator => moderator != target.Author)
                .OrderBy(_ => Random.Shared.Next())
                .First(),
            target.Kind,
            target.Id,
            VerdictKind.Dismissed,
            issuedAt,
            [.. given.Where(report => report.FiledAt < issuedAt).Select(report => report.Id)]);

        // The re-report story: judge one multi-report target midway between its oldest report
        // and the rest, so the dismissal resolves the first wave and the remaining reports
        // re-report the cleared target as the queue's fresh case.
        var story = judgeable
            .Where(candidate => candidate.Reports.Count >= 2)
            .OrderBy(_ => Random.Shared.Next())
            .FirstOrDefault();
        if (story.Target is not null)
        {
            var (oldest, next) = (story.Reports[0].FiledAt, story.Reports[1].FiledAt);
            verdicts.Add(Dismissal(story.Target, story.Reports, oldest + (next - oldest) / 2));
        }

        // Fill up to the two dismissals the log needs with whole-case dismissals, never
        // draining a kind's last pending case — the queue must keep both kinds visible. The
        // story target stays pending through its remaining reports, so it counts as pending.
        var pendingOfKind = judgeable
            .GroupBy(candidate => candidate.Target.Kind)
            .ToDictionary(ofKind => ofKind.Key, ofKind => ofKind.Count());
        foreach (var candidate in judgeable
                     .Where(candidate => candidate != story)
                     .OrderBy(_ => Random.Shared.Next()))
        {
            if (verdicts.Count >= 2) break;
            if (pendingOfKind[candidate.Target.Kind] < 2) continue;
            pendingOfKind[candidate.Target.Kind]--;
            // Issued a beat after the whole case, with a minute of skew per verdict so the
            // instants stay distinct however the report instants fell.
            verdicts.Add(Dismissal(candidate.Target, candidate.Reports,
                candidate.Reports[^1].FiledAt.AddHours(3).AddMinutes(verdicts.Count)));
        }

        return verdicts;
    }
}
