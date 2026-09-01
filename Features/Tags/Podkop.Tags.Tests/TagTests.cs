using Podkop.Tags.Contracts;

namespace Podkop.Tags.Tests;

/// <summary>
///     The one canonical form of a tag (ADR 0009): what folding accepts, what it turns things
///     into, and what it refuses. Every content slice folds its input through exactly this, and
///     the tag-page endpoint folds its route value through it too, so these specs are what makes
///     one tag namespace one namespace. Nothing here touches a database — a value type has no
///     store.
/// </summary>
public class TagTests
{
    [Fact]
    public void A_plain_lowercase_word_folds_to_itself()
    {
        Assert.Equal("polska", Tag.TryFold("polska")?.Name);
    }

    [Fact]
    public void Digits_are_canonical_and_a_tag_may_be_all_of_them()
    {
        // Observed live on Wykop: #2137 and #f1 are real tags
        // (docs/research/wykop-finding-submission-and-tags.md, section 3).
        Assert.Equal("2137", Tag.TryFold("2137")?.Name);
        Assert.Equal("f1", Tag.TryFold("f1")?.Name);
    }

    [Theory]
    [InlineData("POLSKA")]
    [InlineData("Polska")]
    [InlineData("pOlSkA")]
    public void Case_is_folded_away(string input)
    {
        // This is what makes /tag/POLSKA and /tag/polska one page rather than two.
        Assert.Equal("polska", Tag.TryFold(input)?.Name);
    }

    [Theory]
    [InlineData("rozowe_paski")]
    [InlineData("rozowe-paski")]
    [InlineData("rozowe paski")]
    [InlineData("#rozowepaski")]
    [InlineData("  rozowepaski  ")]
    public void Everything_outside_the_canonical_charset_is_folded_away(string input)
    {
        // Wykop's search autocomplete folds the same way: typing "rozowe_" returns exactly the
        // suggestions "rozowe" returns (research doc, section 3).
        Assert.Equal("rozowepaski", Tag.TryFold(input)?.Name);
    }

    [Theory]
    [InlineData("wszechświat", "wszechswiat")]
    [InlineData("Marie Curie-Skłodowska", "mariecuriesklodowska")]
    [InlineData("zażółć", "zazolc")]
    public void Diacritics_fold_to_their_base_letters(string input, string expected)
    {
        // Not stripped — folded: Wykop carries #wszechswiat, never #wszechwiat, and typing
        // "świat" in search returns #swiat (research doc, section 3).
        Assert.Equal(expected, Tag.TryFold(input)?.Name);
    }

    [Fact]
    public void Two_inputs_differing_only_in_what_folding_removes_are_the_same_tag()
    {
        // Records compare by value, so this is the property the whole namespace rests on:
        // whatever a user types, equal tags must be one tag.
        Assert.Equal(Tag.TryFold("Rozowe-Paski"), Tag.TryFold("rozowe_paski"));
    }

    [Fact]
    public void A_tag_may_be_as_long_as_the_limit_allows()
    {
        var atTheLimit = new string('a', Tag.MaxLength);

        Assert.Equal(atTheLimit, Tag.TryFold(atTheLimit)?.Name);
    }

    [Fact]
    public void What_folds_longer_than_the_limit_is_no_tag_at_all()
    {
        // Truncation would silently merge two distinct tags into one; refusing keeps the
        // namespace honest, and the endpoint turns the refusal into a 404.
        Assert.Null(Tag.TryFold(new string('a', Tag.MaxLength + 1)));
    }

    [Fact]
    public void The_length_limit_is_measured_after_folding_not_before()
    {
        var longEnoughOnlyBeforeFolding = new string('a', Tag.MaxLength) + "---";

        Assert.Equal(new string('a', Tag.MaxLength), Tag.TryFold(longEnoughOnlyBeforeFolding)?.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("###")]
    public void Input_carrying_nothing_canonical_is_no_tag_at_all(string? input)
    {
        // Answered, never thrown: a URL that names no tag has to become a 404, and a submission
        // that names none has to become a rejection — neither is an exception.
        Assert.Null(Tag.TryFold(input));
    }

    [Fact]
    public void A_tag_reads_as_its_canonical_name()
    {
        Assert.Equal("polska", Tag.TryFold("POLSKA")!.ToString());
    }
}
