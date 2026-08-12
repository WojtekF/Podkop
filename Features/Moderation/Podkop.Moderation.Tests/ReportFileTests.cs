using System.Globalization;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Tests;

/// <summary>
///     The domain-side filing rules (issue #32), unit-tested on the <see cref="Report.File" />
///     factory: the facts a filed report carries, note trimming and the
///     <see cref="Report.MaxNoteLength" /> cap, and the self-report rejection. Whether the
///     finding exists, whether the point is reportable in the current Statute, and whether the
///     reporter already reported live behind the application ports and are covered by the
///     endpoint tests.
/// </summary>
public class ReportFileTests
{
    private static readonly Guid ReportId = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid PointId = Guid.Parse("aaaa0000-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset FiledAt =
        DateTimeOffset.Parse("2026-07-01T12:00:00Z", CultureInfo.InvariantCulture);

    private static FileReportResult File(string reporter = "ada_lovelace", string findingAuthor = "grace_hopper",
        string? note = "It breaks this rule.") =>
        Report.File(ReportId, reporter, findingAuthor, FindingId, PointId, statuteVersion: 2, note, FiledAt);

    [Fact]
    public void Filing_carries_every_fact_of_the_report()
    {
        var result = File(note: "It breaks this rule.");

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.NotNull(result.Report);
        Assert.Equal(ReportId, result.Report.Id);
        Assert.Equal("ada_lovelace", result.Report.Reporter);
        Assert.Equal(FindingId, result.Report.FindingId);
        Assert.Equal(PointId, result.Report.StatutePointId);
        Assert.Equal(2, result.Report.StatuteVersion);
        Assert.Equal("It breaks this rule.", result.Report.Note);
        Assert.Equal(FiledAt, result.Report.FiledAt);
    }

    [Fact]
    public void The_note_is_stored_trimmed()
    {
        var result = File(note: "  It breaks this rule. \n");

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.Equal("It breaks this rule.", result.Report!.Note);
    }

    [Fact]
    public void A_missing_note_is_stored_as_no_note()
    {
        var result = File(note: null);

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.Null(result.Report!.Note);
    }

    [Fact]
    public void A_whitespace_only_note_is_stored_as_no_note()
    {
        var result = File(note: "   \n\t ");

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.Null(result.Report!.Note);
    }

    [Fact]
    public void A_note_of_exactly_the_cap_is_accepted()
    {
        var result = File(note: new string('x', Report.MaxNoteLength));

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.Equal(Report.MaxNoteLength, result.Report!.Note!.Length);
    }

    [Fact]
    public void A_note_over_the_cap_is_rejected()
    {
        var result = File(note: new string('x', Report.MaxNoteLength + 1));

        Assert.Equal(FileReportOutcome.NoteTooLong, result.Outcome);
        Assert.Null(result.Report);
    }

    [Fact]
    public void The_cap_applies_to_the_trimmed_note()
    {
        // Raw length is over the cap, trimmed length is exactly at it: trimming must happen
        // before validation, so this note is accepted.
        var result = File(note: "  " + new string('x', Report.MaxNoteLength) + "  ");

        Assert.Equal(FileReportOutcome.Filed, result.Outcome);
        Assert.Equal(Report.MaxNoteLength, result.Report!.Note!.Length);
    }

    [Fact]
    public void The_findings_author_cannot_report_their_own_finding()
    {
        var result = File(reporter: "grace_hopper", findingAuthor: "grace_hopper");

        Assert.Equal(FileReportOutcome.OwnFinding, result.Outcome);
        Assert.Null(result.Report);
    }
}
