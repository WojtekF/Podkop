namespace Podkop.Tags.Contracts;

/// <summary>
///     The one canonical form of a tag, shared by every slice that accepts tagged content (ADR
///     0009). It lives in a Contracts project rather than in the Tags slice's Domain because
///     content slices must fold user input through exactly this rule at write time: two copies of
///     the rule would drift, and a drifted copy forks the one tag namespace CONTEXT.md promises.
///     The type carries vocabulary, never behavior beyond it — no store, no identity, no
///     lifecycle: a tag exists exactly as long as content carries it.
///     <para>
///         The canonical form is lowercase ASCII letters and digits, 1–50 characters, and
///         anything a user types is folded into it — the Tag domain-model resolution and the
///         charset observed throughout
///         <c>docs/research/wykop-finding-submission-and-tags.md</c>. Folding is total in one
///         direction only: every input either folds to one canonical tag or to no tag at all, and
///         inputs differing only in what folding removes must land on the same tag, because that
///         is what makes <c>/tag/POLSKA</c> and <c>/tag/polska</c> one page. Input carrying
///         nothing canonical is not a tag and must be answerable as such rather than throwing —
///         the tag-page endpoint turns that answer into a 404, a content slice into a rejected
///         submission.
///     </para>
///     Specified by <c>TagTests</c> in <c>Podkop.Tags.Tests</c>.
/// </summary>
public sealed record Tag
{
    /// <summary>The longest canonical form a tag may take.</summary>
    public const int MaxLength = 50;

    // Assigning constructor only, and private: every way of getting a Tag goes through the
    // folding below, so no caller can mint one that skipped it.
    private Tag(string name) => Name = name;

    /// <summary>The canonical name, without the leading <c>#</c> — the form URLs and rows carry.</summary>
    public string Name { get; }

    /// <summary>
    ///     The tag the given input names, or <c>null</c> when the input names no tag at all.
    /// </summary>
    public static Tag? TryFold(string? input) => throw new NotImplementedException();

    public override string ToString() => Name;
}
