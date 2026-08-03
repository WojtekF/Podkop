using System.Collections.Immutable;

namespace Podkop.Shared.Infrastructure;

/// <summary>
///     The raw material every slice's sample generator draws from until PostgreSQL persistence
///     lands. It lives outside the slices because seed data only looks like one dataset if the
///     slices share it: a comment thread written by people who never author findings, or tags no
///     finding carries, reads as two disconnected fixtures rather than one app. Slices keep
///     owning their own generators (ADR 0003) — this project holds no domain types and no
///     generation logic, only the vocabulary those generators pick from, so sharing it creates no
///     coupling between the slices themselves.
/// </summary>
public static class SampleData
{
    /// <summary>
    ///     Everyone who authors sample content, findings and comments alike. The stub current
    ///     user (ada_lovelace, see <c>StubCurrentUser</c>) is deliberately among them so
    ///     own-content rules — you cannot vote on what you wrote — are observable in the
    ///     running app.
    /// </summary>
    public static readonly ImmutableArray<string> Authors =
    [
        "ada_lovelace",
        "grace_hopper",
        "linus_t",
        "margaret_h",
        "dennis_r",
        "milan_jovanovic",
        "nick_chapsas",
        "matt_pocock",
        "web_dev_simplified",
        "Marie Curie-Skłodowska",
        "Albert Einstein",
        "Ernest Hemingway",
        "Martin Luther",
        "Richard Feynman",
        "Wisława Szymborska",
        "Nelson Mandela",
        "Frances Arnold",
        "Katalin Karikó",
        "Emmanuelle Charpentier"
    ];

    /// <summary>
    ///     Five hundred handles spelled out one by one would drown this file, so <see cref="Voters" />
    ///     is the cross product of these stems and <see cref="VoterSuffixes" /> instead — the shape
    ///     real handles take anyway. Both must stay declared above <see cref="Voters" />: static
    ///     field initializers run top to bottom, and reading them from lower down would hand
    ///     <see cref="BuildVoters" /> two uninitialized (null-backed) arrays.
    /// </summary>
    private static readonly ImmutableArray<string> VoterStems =
    [
        "kuba", "nocny_marek", "dev_null", "zzz", "ptaszek",
        "bartek", "magda", "wojtek_k", "kasia", "tomasz",
        "anka", "rafal", "pawel", "sylwia", "darek",
        "mirek", "grzesiek", "iwona", "lukasz", "natalia",
        "segfault", "null_ptr", "stack_trace", "rubber_duck", "lazy_loader",
        "hot_reload", "dark_mode", "semver", "kebab_case", "tabs_over_spaces",
        "coffee_driven", "ctrl_alt_del", "ping_pong", "yak_shaver", "bikeshedder",
        "off_by_one", "race_condition", "tech_debt", "legacy_code", "prod_hotfix",
        "janusz", "grazyna", "seba", "karyna", "bogdan",
        "halinka", "zenek", "stefan", "jadzia", "mietek"
    ];

    private static readonly ImmutableArray<string> VoterSuffixes =
        ["", "_91", "_86", "_99", "_2000", "_pl", "_dev", "_xd", "_ftw", "_ng"];

    /// <summary>
    ///     The crowd that votes but never writes. Votes are keyed by voter name — one name can
    ///     hold one vote on a finding — so a seed can only show the dig counts a real front page
    ///     carries if it has a matching supply of distinct voters, far more than the handful of
    ///     people worth attributing content to. Deliberately disjoint from <see cref="Authors" />
    ///     so seeding can never hand a finding a vote from its own author, which the domain
    ///     forbids (<c>Finding.SetVote</c>) but a seed writing straight into the aggregate would
    ///     otherwise sneak past. The stub user votes too, but that is the seed's business to
    ///     arrange, not this pool's.
    /// </summary>
    public static readonly ImmutableArray<string> Voters = BuildVoters();

    /// <summary>The tag vocabulary sample findings are tagged from.</summary>
    public static readonly ImmutableArray<string> Tags =
        ["dotnet", "angular", "webdev", "csharp", "typescript", "aspire", "performance", "ui"];

    /// <summary>The sites sample findings link out to.</summary>
    public static readonly ImmutableArray<string> Hosts =
        ["github.com", "news.ycombinator.com", "dev.to", "medium.com", "stackoverflow.blog"];

    /// <summary>
    ///     Filler prose that sample bodies — finding descriptions, comment text — are assembled
    ///     from, a sentence at a time.
    /// </summary>
    public static readonly ImmutableArray<string> Lines =
    [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.",
        "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos."
    ];

    private static ImmutableArray<string> BuildVoters()
    {
        return
        [
            .. VoterStems
                .SelectMany(_ => VoterSuffixes, (stem, suffix) => stem + suffix)
                .Except(Authors)
        ];
    }
}
