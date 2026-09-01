namespace Podkop.Tags.Infrastructure;

/// <summary>
///     One piece of sample content as the Tags seed generator sees it: the announcement a content
///     slice would have made about it, in primitive form. The Tags slice may not look into
///     Findings (ADR 0003), so the coordinator that sees every slice — the migration worker —
///     projects the sample content into these rows and hands them over, exactly as
///     <c>SampleSeed</c> projects report targets for the Moderation generators.
/// </summary>
/// <param name="ContentType">One of <c>Podkop.Tags.Contracts.TaggedContentTypes</c>.</param>
/// <param name="Tags">The content's tags as its own slice holds them, before folding.</param>
public sealed record SampleTaggedContent(
    string ContentType,
    Guid ContentId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);
