namespace Podkop.Findings.Domain;

public sealed record FindingPromoted(Guid FindingId, DateTimeOffset PromotedAt) : IDomainEvent;
