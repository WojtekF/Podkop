using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Podkop.FindingComments.Application;
using Podkop.FindingComments.Infrastructure;
using Podkop.Findings.Domain;
using Podkop.Shared.Testing;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     Posting a comment moves the finding's comment count across the slice boundary (issue
///     #17); since the findings live in PostgreSQL that moved count must also outlive the
///     request that moved it (issues #67, #96). The finding side here is the production wiring
///     over the real database — only the discussion store is doubled — so the count the
///     <c>CommentPosted</c> consumer increments on its loaded aggregate is durable only if the
///     use case actually commits it: a count that was only ever moved in memory answers the
///     posting request correctly and is gone by the next one, which is exactly what this spec
///     refuses to let pass.
/// </summary>
[Collection(FindingsDatabaseCollection.Name)]
public class PostCommentDurabilityTests(FindingsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static Finding CreateFinding(Guid id, int commentCount) =>
        new(
            id: id,
            title: "A finding under discussion",
            description: "The finding the comments land under.",
            source: new Uri("https://blog.example.org/posts/42"),
            thumbnail: null,
            author: "grace_hopper",
            tags: ["angular"],
            createdAt: At("2026-07-08T03:30:00Z"),
            promotedAt: At("2026-07-08T09:30:00Z"),
            commentCount: commentCount);

    private async Task<WebApplicationFactory<Program>> AppOverSeededFindingAsync(int commentCount)
    {
        await using (var context = database.CreateDbContext())
        {
            context.Findings.Add(CreateFinding(FindingId, commentCount));
            await context.SaveChangesAsync();
        }

        // Only the discussion store is doubled (empty, in memory); the findings side answers
        // from the real database through whatever the production wiring resolves.
        return new WebApplicationFactory<Program>()
            .WithPodkopDatabase(database.ConnectionString)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton<ICommentRepository>(provider =>
                    new InMemoryCommentRepository([], provider.GetRequiredService<IPublisher>()))));
    }

    [Fact]
    public async Task A_posted_comments_count_survives_into_the_next_request()
    {
        using var factory = await AppOverSeededFindingAsync(commentCount: 7);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/findings/{FindingId}/comments",
            new { text = "A fresh take." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // A second request in its own scope over its own context: only a count the posting
        // request actually made durable can still read 8 here.
        var finding = await client.GetFromJsonAsync<FindingResponse>($"/api/findings/{FindingId}");

        Assert.NotNull(finding);
        Assert.Equal(8, finding.CommentCount);
    }

    private sealed record FindingResponse(Guid Id, int CommentCount);
}
