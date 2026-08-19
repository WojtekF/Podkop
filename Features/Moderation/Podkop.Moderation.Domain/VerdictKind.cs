namespace Podkop.Moderation.Domain;

/// <summary>
///     How a Verdict rules on its Case (CONTEXT.md): the reported content violates the cited
///     Statute (Upheld) or it does not (Dismissed). The full glossary vocabulary is declared up
///     front, but only <see cref="Dismissed" /> ships with issue #35 — issue #36 on wire
///     <see cref="Upheld" /> to the actions it triggers.
/// </summary>
public enum VerdictKind
{
    Upheld,
    Dismissed
}
