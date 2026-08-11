namespace Podkop.Documents.Domain;

/// <summary>
///     A numbered, titled section of one Privacy Policy version, carrying its prose paragraphs in
///     reading order.
/// </summary>
public sealed record PolicySection(int Number, string Title, IReadOnlyList<string> Paragraphs);
