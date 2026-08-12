namespace Podkop.Moderation.Domain;

/// <summary>
///     A member's formal claim that a specific finding violates a specific reportable Statute
///     Point, optionally explained with a short note (CONTEXT.md). A report cites the point by
///     its stable id and pins the Statute version current at filing time, so amendments never
///     falsify or orphan what was filed (ADR 0006). Reports feed moderation only — they are
///     invisible to regular users and never touch a score, vote, or promotion (ADR 0008,
///     issue #32).
/// </summary>
public sealed class Report
{
    /// <summary>The most text one report note may carry (issue #32); longer notes are rejected.</summary>
    public const int MaxNoteLength = 500;

    public Report(
        Guid id,
        string reporter,
        Guid findingId,
        Guid statutePointId,
        int statuteVersion,
        string? note,
        DateTimeOffset filedAt)
    {
        Id = id;
        Reporter = reporter;
        FindingId = findingId;
        StatutePointId = statutePointId;
        StatuteVersion = statuteVersion;
        Note = note;
        FiledAt = filedAt;
    }

    public Guid Id { get; }
    public string Reporter { get; }
    public Guid FindingId { get; }
    public Guid StatutePointId { get; }
    public int StatuteVersion { get; }
    public string? Note { get; }
    public DateTimeOffset FiledAt { get; }

    /// <summary>
    ///     Files a new report (issue #32). The factory owns the rules decidable from its own
    ///     arguments: the finding's author can never report their own finding, and the note is
    ///     trimmed before validation and storage — a note that is empty after trimming is stored
    ///     as no note at all, and a trimmed note longer than <see cref="MaxNoteLength" />
    ///     characters is rejected. Whether the finding exists, whether the cited point is a
    ///     reportable point of the current Statute, and whether the reporter already reported
    ///     this finding are lookups, checked where the ports and repository are available.
    /// </summary>
    public static FileReportResult File(
        Guid id,
        string reporter,
        string findingAuthor,
        Guid findingId,
        Guid statutePointId,
        int statuteVersion,
        string? note,
        DateTimeOffset filedAt)
    {
        if (reporter == findingAuthor) return new FileReportResult(FileReportOutcome.OwnFinding, null);

        var trimmedNote = note?.Trim();
        if (trimmedNote?.Length > MaxNoteLength) return new FileReportResult(FileReportOutcome.NoteTooLong, null);

        return new FileReportResult(FileReportOutcome.Filed,
            new Report(
                id,
                reporter,
                findingId,
                statutePointId,
                statuteVersion,
                string.IsNullOrWhiteSpace(trimmedNote) ? null : trimmedNote,
                filedAt));
    }
}
