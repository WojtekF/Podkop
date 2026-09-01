using Podkop.Shared.Domain;
using Podkop.Tags.Contracts;

namespace Podkop.Findings.Domain;

public sealed class Finding : AggregateRoot
{
    private readonly List<FindingVoteEntry> _votes = [];

    public Finding(Guid id,
        string title,
        string description,
        Uri source,
        Uri? thumbnail,
        string author,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset? promotedAt,
        int commentCount,
        IReadOnlyDictionary<string, FindingVote>? votes = null)
    {
        Id = id;
        Title = title;
        Description = description;
        Source = source;
        Thumbnail = thumbnail;
        Author = author;
        Tags = tags;
        CreatedAt = createdAt;
        PromotedAt = promotedAt;
        CommentCount = commentCount;
        _votes = votes is null
            ? []
            : votes.Select(v => new FindingVoteEntry(v.Key, v.Value.Side, v.Value.Reason)).ToList();
    }

    private Finding()
    {
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Description { get; }
    public Uri Source { get; }
    public Uri? Thumbnail { get; }
    public string Author { get; }
    public IReadOnlyList<string> Tags { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PromotedAt { get; private set; }
    public int DigCount => _votes.Count(vote => vote.Side == FindingVoteSide.Dig);
    public int BuryCount => _votes.Count(vote => vote.Side == FindingVoteSide.Bury);
    public int CommentCount { get; private set; }

    public bool IsPromoted => PromotedAt is not null;
    public int NetScore => DigCount - BuryCount;

    /// <summary>
    ///     One-way promotion to the Main Page (ADR 0001): stamps <see cref="PromotedAt" /> and raises
    ///     <see cref="FindingPromoted" />. Idempotent — promoting an already-promoted finding changes
    ///     nothing and raises no second event.
    /// </summary>
    public void Promote(DateTimeOffset promotedAt)
    {
        if (IsPromoted) return;
        PromotedAt = promotedAt;
        Raise(new FindingPromoted(Id, promotedAt));
    }

    /// <summary>
    ///     Records the voter's vote (issue #15): a fresh dig or bury, or a one-click switch to the
    ///     other side; setting the side already held changes nothing. A bury must carry a
    ///     <see cref="BuryReason" />; a bury without one is rejected. The finding's own author can
    ///     never vote on it, so scores can't be self-inflated. The dig and bury counts and the
    ///     tracked votes must stay consistent with each other; the bury count never leaves the
    ///     aggregate.
    /// </summary>
    public DigBuryOutcome SetVote(string voter, FindingVoteSide side, BuryReason? reason)
    {
        if (voter == Author) return DigBuryOutcome.OwnFinding;
        if (side == FindingVoteSide.Bury && reason is null) return DigBuryOutcome.BuryReasonRequired;
        _votes.RemoveAll(v => v.Voter == voter);
        _votes.Add(new FindingVoteEntry(voter, side, side == FindingVoteSide.Bury ? reason : null));
        return DigBuryOutcome.Applied;
    }


    /// <summary>
    ///     Withdraws the voter's vote (issue #15), freeing the count it was held in.
    /// </summary>
    public WithdrawOutcome WithdrawVote(string voter)
    {
        if (voter == Author) return WithdrawOutcome.OwnFinding;

        _votes.RemoveAll(v => v.Voter == voter);
        return WithdrawOutcome.Applied;
    }

    /// <summary>
    ///     Sets the finding's tags — the write-time seam every tagged submission and every later
    ///     edit of the set goes through (issue #77). What the user typed is not what the finding
    ///     carries: each input folds through <see cref="Tag" />, the one canonical form the whole
    ///     platform shares (ADR 0009), so the finding joins exactly the tags its tags name and no
    ///     variant spellings of them. What a submission that names no usable tag at all should
    ///     leave the finding carrying, and what a repeated tag should count as, are part of the
    ///     same decision.
    ///     <para>
    ///         The resulting set is announced, not just stored: the finding raises
    ///         <see cref="FindingTagsChanged" />, which infrastructure translates into the public
    ///         announcement the Tags slice indexes (ADR 0011). The announcement carries the
    ///         finding's own <see cref="CreatedAt" /> — never the time of the edit — so re-tagging
    ///         an old finding never jumps it to the top of a tag page.
    ///     </para>
    /// </summary>
    public void SetTags(IReadOnlyList<string> tags) => throw new NotImplementedException();

    /// <summary>
    ///     Announces that this finding is gone (issue #77), raising <see cref="FindingRemoved" />
    ///     so the tag namespace stops listing it (ADR 0011). Deliberately narrow: what removal
    ///     means <i>inside</i> this slice — whether a removed finding still exists, still shows,
    ///     still counts — is nobody's decision yet and no state here records it; this is the seam
    ///     the tag namespace needs, and the ticket that gives Findings a removal will grow it.
    /// </summary>
    public void Remove() => throw new NotImplementedException();

    // method exposed for seeding purpose only.
    public void UpdateCommentCount(int commentCount) => CommentCount = commentCount;

    /// <summary>
    ///     Counts one newly posted comment or reply (issue #17) — the Findings-side effect of
    ///     the FindingComments slice's CommentPosted contract event.
    /// </summary>
    public void IncrementCommentCount() => CommentCount++;

    public FindingVoteSide? VoteBy(string voter) =>
        _votes.SingleOrDefault(v => v.Voter == voter)?.Side;
}
