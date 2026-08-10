using Podkop.FindingComments.Domain;

namespace Podkop.FindingComments.Application;

public static class CommentExtensions
{
    public static CommentReply ToCommentReply(this Comment reply, string currentUser) =>
        new(
            reply.Id,
            reply.Author,
            reply.Text,
            reply.UpvoteCount,
            reply.DownvoteCount,
            MyVote: reply.VoteBy(currentUser).ToApiString(),
            reply.CreatedAt);
}
