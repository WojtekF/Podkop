namespace Podkop.Documents.Domain;

/// <summary>
///     A numbered section of one Statute version, holding its numbered Statute Points. Sections
///     give the document its citation shape: point 1 of section 2 is cited as "2.1".
/// </summary>
public sealed record StatuteSection(int Number, string Title, IReadOnlyList<StatutePoint> Points);
