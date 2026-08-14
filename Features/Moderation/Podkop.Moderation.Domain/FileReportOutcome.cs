namespace Podkop.Moderation.Domain;

public enum FileReportOutcome
{
    /// <summary>The report was filed — 201 with the my-report state.</summary>
    Filed,

    /// <summary>
    ///     No content of the targeted kind has that id — 404; the endpoint names the kind in the
    ///     problem type (<c>podkop:problem:unknown-finding</c> / <c>podkop:problem:unknown-comment</c>).
    /// </summary>
    UnknownTarget,

    /// <summary>
    ///     The reporter authored the targeted content — 400; the endpoint names the kind in the
    ///     problem type (<c>podkop:problem:own-finding</c> / <c>podkop:problem:own-comment</c>).
    /// </summary>
    OwnContent,

    /// <summary>
    ///     The cited point is not a reportable point of the Statute version currently in force
    ///     (including when no version is in force at all) — 400,
    ///     <c>podkop:problem:point-not-reportable</c>.
    /// </summary>
    NotReportablePoint,

    /// <summary>
    ///     The reporter already reported this target — one report per user per target — 409,
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
