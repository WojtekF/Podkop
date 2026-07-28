using Podkop.Findings.Domain;

namespace Podkop.Findings.Infrastructure;

/// <summary>
/// Development seed data until PostgreSQL persistence lands. Roughly two thirds of the
/// findings are promoted; some have no thumbnail. Since issue #16 the seeded comment threads
/// are the authority for comment counts: a finding's CommentCount may no longer be invented
/// here — the composition root's SampleSeed lines this generator up with the seeded discussion.
/// </summary>
public static class SampleFindings
{
    private static readonly string[] Authors = ["ada_lovelace", "grace_hopper", "linus_t", "margaret_h", "dennis_r"];
    private static readonly string[] Hosts = ["github.com", "news.ycombinator.com", "dev.to", "medium.com", "stackoverflow.blog"];
    private static readonly string[] AllTags = ["dotnet", "angular", "webdev", "csharp", "typescript", "aspire", "performance", "ui"];

    private static readonly string[] Sentences =
    [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium.",
        "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos.",
    ];

    public static IReadOnlyList<Finding> Generate(int count = 30)
    {
        var now = DateTimeOffset.UtcNow;

        return Enumerable.Range(1, count).Select(index =>
        {
            var createdAt = now.AddHours(-Random.Shared.Next(2, 96));
            var promoted = index % 3 != 0;
            var digCount = promoted ? Random.Shared.Next(50, 1500) : Random.Shared.Next(0, 49);

            return new Finding(
                id: Guid.NewGuid(),
                title: $"Sample finding {index}",
                description: string.Join(" ", Random.Shared.GetItems(Sentences, Random.Shared.Next(1, 4))),
                source: new Uri($"https://{Hosts[Random.Shared.Next(Hosts.Length)]}/article/{index}"),
                thumbnail: index % 5 == 0 ? null : new Uri($"https://picsum.photos/id/{index * 10}/220/142"),
                author: Authors[Random.Shared.Next(Authors.Length)],
                tags: Random.Shared.GetItems(AllTags, Random.Shared.Next(1, 4)).Distinct().ToArray(),
                createdAt: createdAt,
                promotedAt: promoted ? createdAt.AddHours(Random.Shared.Next(1, 24)) : null,
                digCount: digCount,
                buryCount: Random.Shared.Next(0, 20),
                commentCount: Random.Shared.Next(0, 250));
        }).ToArray();
    }
}
