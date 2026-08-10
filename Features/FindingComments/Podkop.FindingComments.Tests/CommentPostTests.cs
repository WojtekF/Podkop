using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Tests;

/// <summary>
///     The Comment aggregate's creation rules (issue #17), unit-tested at the aggregate seam —
///     kept minimal per #13's testing decisions; the HTTP seam carries the spec. Text is
///     trimmed before validation and storage; empty-after-trim and over-5000 are rejected; a
///     successful post raises CommentAdded. The depth invariant is here too — the factory
///     receives the loaded parent and rejects a reply to a reply; whether the parent exists at
///     all is a lookup, specified at the HTTP seam.
/// </summary>
public class CommentPostTests
{
    private static readonly Guid CommentId = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    private static readonly Guid FindingId = Guid.Parse("0d4f9a3e-1111-4222-8333-444455556666");
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-07-08T10:00:00+00:00");

    private static PostCommentResult Post(string? text, Comment? parent = null)
    {
        return Comment.Post(CommentId, FindingId, parent, "ada_lovelace", text, CreatedAt);
    }

    [Fact]
    public void A_valid_post_yields_the_comment_with_its_facts()
    {
        var result = Post("A fresh take.");

        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
        Assert.NotNull(result.Comment);
        Assert.Equal(CommentId, result.Comment.Id);
        Assert.Equal(FindingId, result.Comment.FindingId);
        Assert.Null(result.Comment.ParentCommentId);
        Assert.Equal("ada_lovelace", result.Comment.Author);
        Assert.Equal("A fresh take.", result.Comment.Text);
        Assert.Equal(CreatedAt, result.Comment.CreatedAt);
        Assert.Equal(0, result.Comment.UpvoteCount);
        Assert.Equal(0, result.Comment.DownvoteCount);
    }

    [Fact]
    public void A_valid_post_raises_CommentAdded_with_the_ids()
    {
        var result = Post("A fresh take.");

        var raised = Assert.Single(result.Comment!.DomainEvents);
        var added = Assert.IsType<CommentAdded>(raised);
        Assert.Equal(CommentId, added.CommentId);
        Assert.Equal(FindingId, added.FindingId);
    }

    [Fact]
    public void A_reply_carries_its_parent_id()
    {
        var parent = new Comment(
            Guid.Parse("c0000000-0000-4000-8000-00000000000a"),
            FindingId, parentCommentId: null, "grace_hopper", "The original take.", CreatedAt);

        var result = Post("An answer.", parent);

        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
        Assert.Equal(parent.Id, result.Comment!.ParentCommentId);
        Assert.True(result.Comment.IsReply);
    }

    [Fact]
    public void A_reply_to_a_reply_is_rejected()
    {
        var reply = new Comment(
            Guid.Parse("c0000000-0000-4000-8000-00000000000b"),
            FindingId, parentCommentId: Guid.Parse("c0000000-0000-4000-8000-00000000000a"),
            "grace_hopper", "An answer.", CreatedAt);

        var result = Post("A counter.", reply);

        Assert.Equal(PostCommentOutcome.ParentIsAReply, result.Outcome);
        Assert.Null(result.Comment);
    }

    [Fact]
    public void Text_is_trimmed_before_storage()
    {
        var result = Post("  A fresh take. \n");

        Assert.Equal("A fresh take.", result.Comment!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \n\t ")]
    public void Text_empty_after_trimming_is_rejected(string? text)
    {
        var result = Post(text);

        Assert.Equal(PostCommentOutcome.EmptyText, result.Outcome);
        Assert.Null(result.Comment);
    }

    [Fact]
    public void Text_over_the_cap_is_rejected()
    {
        var result = Post(new string('x', Comment.MaxTextLength + 1));

        Assert.Equal(PostCommentOutcome.TextTooLong, result.Outcome);
        Assert.Null(result.Comment);
    }

    [Fact]
    public void Text_of_exactly_the_cap_is_accepted()
    {
        var result = Post(new string('x', Comment.MaxTextLength));

        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
    }

    [Fact]
    public void The_length_cap_applies_to_the_trimmed_text()
    {
        // 5000 real characters wrapped in whitespace: the padding must not push it over.
        var result = Post("  " + new string('x', Comment.MaxTextLength) + "  ");

        Assert.Equal(PostCommentOutcome.Posted, result.Outcome);
        Assert.Equal(Comment.MaxTextLength, result.Comment!.Text.Length);
    }
}
