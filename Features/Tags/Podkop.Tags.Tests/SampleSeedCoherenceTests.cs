using Podkop.Findings.Infrastructure;
using Podkop.Tags.Contracts;
using Podkop.Tags.Infrastructure;

namespace Podkop.Tags.Tests;

/// <summary>
///     The seed pact between the Tags index and the findings it indexes (issue #77). The two
///     halves are generated in the same worker but from different slices' generators, and nothing
///     patches them up afterwards — so this suite holds them to each other directly: the index the
///     worker seeds must describe exactly the findings the findings seed persisted, under exactly
///     the tags those findings carry, or the seeded app comes up with tag pages that 404 or list
///     content that is not there. Reaching into another slice's Infrastructure is a Tests-project
///     privilege; production code never may (ADR 0003).
/// </summary>
public class SampleSeedCoherenceTests
{
    /// <summary>The projection the migration worker makes — the same one, so the pact is the real one.</summary>
    private static IReadOnlyList<SampleTaggedContent> SampleContent() =>
    [
        .. SampleFindings.Generate().Select(finding => new SampleTaggedContent(
            TaggedContentTypes.Finding, finding.Id, finding.Tags, finding.CreatedAt))
    ];

    [Fact]
    public void Every_seeded_finding_is_in_the_seeded_index()
    {
        var findings = SampleFindings.Generate();

        var index = SampleTagMemberships.GenerateFor(SampleContent());

        Assert.Equal(
            findings.Select(finding => finding.Id).Order(),
            index.Select(row => row.ContentId).Distinct().Order());
    }

    [Fact]
    public void Every_indexed_row_names_a_tag_its_finding_actually_carries()
    {
        var findings = SampleFindings.Generate().ToDictionary(finding => finding.Id);

        var index = SampleTagMemberships.GenerateFor(SampleContent());

        Assert.All(index, row => Assert.Contains(
            row.Tag,
            findings[row.ContentId].Tags.Select(tag => Tag.TryFold(tag)!.Name)));
    }

    [Fact]
    public void Every_tag_a_seeded_finding_carries_has_a_page_that_lists_it()
    {
        // The other direction: a tag chip on a seeded card must not lead to a 404.
        var findings = SampleFindings.Generate();

        var index = SampleTagMemberships.GenerateFor(SampleContent());

        Assert.All(findings, finding => Assert.All(
            finding.Tags,
            tag => Assert.Contains(
                index,
                row => row.ContentId == finding.Id && row.Tag == Tag.TryFold(tag)!.Name)));
    }

    [Fact]
    public void The_index_carries_each_findings_own_creation_time()
    {
        var findings = SampleFindings.Generate().ToDictionary(finding => finding.Id);

        var index = SampleTagMemberships.GenerateFor(SampleContent());

        Assert.All(index, row => Assert.Equal(findings[row.ContentId].CreatedAt, row.CreatedAt));
    }
}
