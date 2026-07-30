namespace Podkop.Findings.Domain;

/// <summary>
///     One voter's vote on a finding: which <see cref="FindingVoteSide" /> they took and, for a
///     bury, the <see cref="BuryReason" /> behind it (issue #15). A dig carries no reason. The
///     side is public (it feeds the dig count and highlights the reader's own vote); the reason
///     is private and never leaves the aggregate.
/// </summary>
public sealed record FindingVote(FindingVoteSide Side, BuryReason? Reason);
