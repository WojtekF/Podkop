namespace Podkop.Findings.Domain;

public sealed record FindingVoteEntry(string Voter, FindingVoteSide Side, BuryReason? Reason);
