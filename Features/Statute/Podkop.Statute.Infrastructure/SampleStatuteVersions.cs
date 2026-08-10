using Podkop.Statute.Domain;

namespace Podkop.Statute.Infrastructure;

/// <summary>
///     Development seed for the Statute until PostgreSQL persistence lands: the actual shipped
///     content of the document, since amendments ship as code (issue #30). The seed must hold at
///     least two versions so historical retrieval is observable, with exactly one of them in
///     force today. Every version tells what the service is for, lays out the rules of conduct,
///     and states the consequences of breaking them; only the conduct-rule points are flagged
///     reportable, and a point that survives an amendment keeps its id (ADR 0006).
/// </summary>
public static class SampleStatuteVersions
{
    public static IReadOnlyList<StatuteVersion> Generate() => throw new NotImplementedException();
}
