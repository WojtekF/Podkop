namespace Podkop.Statute.Domain;

/// <summary>
///     One version of the Statute — the document stating what the service is for, the rules of
///     conduct, and the consequences of breaking them (CONTEXT.md). Versions are immutable: an
///     amendment ships as a whole new <see cref="StatuteVersion" /> with a higher version number
///     and its own effective-from date, and old versions remain readable (ADR 0006, issue #30).
/// </summary>
public sealed class StatuteVersion
{
    public StatuteVersion(int version, DateTimeOffset effectiveFrom, IReadOnlyList<StatuteSection> sections)
    {
        Version = version;
        EffectiveFrom = effectiveFrom;
        Sections = sections;
    }

    public int Version { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public IReadOnlyList<StatuteSection> Sections { get; }
}
