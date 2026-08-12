namespace Podkop.Moderation.Domain;

public enum FileReportOutcome
{
    /// <summary>The report was filed — 201 with the my-report state.</summary>
    Filed,

    /// <summary>No finding has that id — 404, <c>podkop:problem:unknown-finding</c>.</summary>
    UnknownFinding,

    /// <summary>The reporter authored the finding — 400, <c>podkop:problem:own-finding</c>.</summary>
    OwnFinding,

    /// <summary>
    ///     The cited point is not a reportable point of the Statute version currently in force
    ///     (including when no version is in force at all) — 400,
    ///     <c>podkop:problem:point-not-reportable</c>.
    /// </summary>
    NotReportablePoint,

    /// <summary>
    ///     The reporter already reported this finding — one report per user per finding — 409,
    ///     <c>podkop:problem:already-reported</c>.
    /// </summary>
    AlreadyReported,

    /// <summary>The trimmed note is over the length cap — 400, <c>podkop:problem:report-note-too-long</c>.</summary>
    NoteTooLong
}

/// <summary>
///     Outcome of the domain-side filing rules: either the report was created and
///     <see cref="Report" /> carries it, or <see cref="Outcome" /> names the refusal and no
///     report exists.
/// </summary>
public sealed record FileReportResult(FileReportOutcome Outcome, Report? Report);
