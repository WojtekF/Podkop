using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Podkop.FindingComments.Contracts;
using Podkop.FindingComments.Domain;
using Podkop.FindingComments.Infrastructure;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The contract-event half of the commit seam (issue #68, ADR 0003): the slice announces
///     <see cref="CommentPosted" /> by translating what the aggregate raised — and only after the
///     commit made it durable. Nothing is announced for work that never committed or failed to
///     commit, nothing is announced that the aggregate did not raise, and one committed post is
///     announced exactly once, however many times the scope commits. The publisher is the request
///     scope's own (the issue #96 lesson), so one recording publisher stands in for the whole
///     use case here, wherever the implementation publishes from.
/// </summary>
[Collection(FindingCommentsDatabaseCollection.Name)]
public class CommentPostedPublicationTests(FindingCommentsPostgresDatabase database) : IAsyncLifetime
{
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000101");

    private readonly RecordingPublisher _publisher = new();

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>A comment the way the use case creates one — through the factory, so it raised its event.</summary>
    private static Comment PostedComment(Guid id)
    {
        var result = Comment.Post(id, FindingId, null, "grace_hopper", "A take worth judging.", At("2026-07-08T10:00:00Z"));
        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
        return result.Comment!;
    }

    /// <summary>A comment the way rehydration creates one — through the constructor, so it raised nothing.</summary>
    private static Comment RehydratedComment(Guid id) =>
        new(id, FindingId, null, "grace_hopper", "A take worth judging.", At("2026-07-08T10:00:00Z"));

    private async Task GivenComments(params Comment[] comments)
    {
        await using var context = database.CreateDbContext();
        context.Comments.AddRange(comments);
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     One use case's worth of work, wired the way a request scope is: repository and unit of
    ///     work over the same context, every publication — wherever it happens — reaching the same
    ///     recording publisher.
    /// </summary>
    private async Task InOneUseCase(Func<EfCommentRepository, EfUnitOfWork, Task> useCase)
    {
        await using var context = database.CreateDbContext();
        await useCase(
            new EfCommentRepository(context),
            new EfUnitOfWork(context, _publisher));
    }

    [Fact]
    public async Task A_committed_post_announces_one_comment_posted_carrying_the_aggregates_facts()
    {
        await InOneUseCase(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
        });

        var posted = Assert.Single(_publisher.Published.OfType<CommentPosted>());
        Assert.Equal(CommentId, posted.CommentId);
        Assert.Equal(FindingId, posted.FindingId);
    }

    [Fact]
    public async Task An_add_alone_announces_nothing_the_commit_is_the_announcement()
    {
        // Consumers act on the announcement (Findings counts the comment); an add that never
        // commits must therefore stay silent, or the count drifts from the truth.
        await InOneUseCase(async (repository, _) =>
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None));

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_failed_commit_announces_nothing()
    {
        // The same guarantee under failure: the commit blows up (the id is already taken), so no
        // consumer may ever have heard of the comment that did not make it in.
        await GivenComments(RehydratedComment(CommentId));

        await InOneUseCase(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                unitOfWork.CommitAsync(CancellationToken.None));
        });

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_second_commit_does_not_announce_the_post_again()
    {
        await InOneUseCase(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(PostedComment(CommentId), CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
        });

        Assert.Single(_publisher.Published.OfType<CommentPosted>());
    }

    [Fact]
    public async Task Adding_a_comment_that_raised_nothing_announces_nothing()
    {
        // Translation, not fabrication: the announcement exists because the aggregate raised
        // CommentAdded — never merely because AddAsync was called with a comment.
        await InOneUseCase(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(RehydratedComment(CommentId), CancellationToken.None);
            await unitOfWork.CommitAsync(CancellationToken.None);
        });

        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_committed_vote_announces_nothing()
    {
        // Votes raise no domain event today, so their commits must stay silent — a publication
        // keyed to committing rather than to what was raised would announce phantom posts here.
        await GivenComments(RehydratedComment(CommentId));

        await InOneUseCase(async (repository, unitOfWork) =>
        {
            var comment = await repository.GetByIdAsync(CommentId, CancellationToken.None);
            Assert.Equal(ActionOutcome.Applied, comment!.SetVote("ada_lovelace", VoteDirection.Up));
            await unitOfWork.CommitAsync(CancellationToken.None);
        });

        Assert.Empty(_publisher.Published);
    }

    /// <summary>Captures every publication of the use case, in order.</summary>
    private sealed class RecordingPublisher : IPublisher
    {
        private readonly List<object> _published = [];

        public IReadOnlyList<object> Published => _published;

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            _published.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            _published.Add(notification);
            return Task.CompletedTask;
        }
    }
}
