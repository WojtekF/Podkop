using Podkop.Documents.Domain;

namespace Podkop.Documents.Infrastructure;

/// <summary>
///     Development seed for the Statute until PostgreSQL persistence lands: the actual shipped
///     content of the document, since amendments ship as code (issue #30). Two versions ship —
///     the original and the amendment in force today, which rewords the spam rule and adds the
///     personal-data rule. A point that survives the amendment keeps its id (ADR 0006); only the
///     conduct rules of section 2 are reportable.
/// </summary>
public static class SampleStatuteVersions
{
    // The stable identities a Report cites (ADR 0006): the same point carries the same id in
    // every version, however its number or wording changes.
    private static readonly Guid WhatPodkopIs = Guid.Parse("57a70000-0000-4000-8000-000000000101");
    private static readonly Guid AcceptingTheStatute = Guid.Parse("57a70000-0000-4000-8000-000000000102");
    private static readonly Guid NoSpam = Guid.Parse("57a70000-0000-4000-8000-000000000201");
    private static readonly Guid NoFalseInformation = Guid.Parse("57a70000-0000-4000-8000-000000000202");
    private static readonly Guid NoHatefulContent = Guid.Parse("57a70000-0000-4000-8000-000000000203");
    private static readonly Guid NoUnlawfulContent = Guid.Parse("57a70000-0000-4000-8000-000000000204");
    private static readonly Guid NoPersonalData = Guid.Parse("57a70000-0000-4000-8000-000000000205");
    private static readonly Guid RemovalAndRedaction = Guid.Parse("57a70000-0000-4000-8000-000000000301");
    private static readonly Guid TemporaryBans = Guid.Parse("57a70000-0000-4000-8000-000000000302");
    private static readonly Guid ModerationLog = Guid.Parse("57a70000-0000-4000-8000-000000000303");

    public static IReadOnlyList<StatuteVersion> Generate() => [Version1(), Version2()];

    private static StatuteVersion Version1() => new(
        1,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(WhatPodkopIs, 1,
                    "Podkop is a community where users share findings — links to content worth the " +
                    "community's attention — discuss them, and judge them by digging and burying; the " +
                    "best findings are promoted to the Main Page.", false),
                new StatutePoint(AcceptingTheStatute, 2,
                    "Using Podkop means accepting this Statute. The version in force and every earlier " +
                    "version remain available to read.", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                new StatutePoint(NoSpam, 1,
                    "Do not post spam: unsolicited advertising, repetitive content, or link schemes.",
                    true),
                new StatutePoint(NoFalseInformation, 2,
                    "Do not present false information as fact.", true),
                new StatutePoint(NoHatefulContent, 3,
                    "Do not post hateful, harassing, or threatening content.", true),
                new StatutePoint(NoUnlawfulContent, 4,
                    "Do not post content that is unlawful or that links to unlawful material.", true),
            ]),
            new StatuteSection(3, "Consequences of breaking the rules",
            [
                new StatutePoint(RemovalAndRedaction, 1,
                    "A finding or comment that breaks a rule of section 2 may be removed, or have its " +
                    "offending text redacted, by a moderator — always citing the point broken.", false),
                new StatutePoint(TemporaryBans, 2,
                    "Repeated or serious violations may lead to a temporary ban of 1, 7, or 30 days, " +
                    "during which the account cannot post, comment, vote, or report.", false),
                new StatutePoint(ModerationLog, 3,
                    "Every moderation action is recorded in an internal moderation log.", false),
            ]),
        ]);

    private static StatuteVersion Version2() => new(
        2,
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        [
            new StatuteSection(1, "Purpose of the service",
            [
                new StatutePoint(WhatPodkopIs, 1,
                    "Podkop is a community where users share findings — links to content worth the " +
                    "community's attention — discuss them, and judge them by digging and burying; the " +
                    "best findings are promoted to the Main Page.", false),
                new StatutePoint(AcceptingTheStatute, 2,
                    "Using Podkop means accepting this Statute. The version in force and every earlier " +
                    "version remain available to read.", false),
            ]),
            new StatuteSection(2, "Rules of conduct",
            [
                // Reworded by the amendment — same id, same number, sharper text.
                new StatutePoint(NoSpam, 1,
                    "Do not post spam: unsolicited advertising, repetitive content, link schemes, or " +
                    "flooding the Upcoming feed with self-promotion.", true),
                new StatutePoint(NoFalseInformation, 2,
                    "Do not present false information as fact.", true),
                new StatutePoint(NoHatefulContent, 3,
                    "Do not post hateful, harassing, or threatening content.", true),
                new StatutePoint(NoUnlawfulContent, 4,
                    "Do not post content that is unlawful or that links to unlawful material.", true),
                // Added by the amendment.
                new StatutePoint(NoPersonalData, 5,
                    "Do not publish another person's personal data without their consent.", true),
            ]),
            new StatuteSection(3, "Consequences of breaking the rules",
            [
                new StatutePoint(RemovalAndRedaction, 1,
                    "A finding or comment that breaks a rule of section 2 may be removed, or have its " +
                    "offending text redacted, by a moderator — always citing the point broken.", false),
                new StatutePoint(TemporaryBans, 2,
                    "Repeated or serious violations may lead to a temporary ban of 1, 7, or 30 days, " +
                    "during which the account cannot post, comment, vote, or report.", false),
                new StatutePoint(ModerationLog, 3,
                    "Every moderation action is recorded in an internal moderation log.", false),
            ]),
        ]);
}
