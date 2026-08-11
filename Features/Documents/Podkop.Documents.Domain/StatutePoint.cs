namespace Podkop.Documents.Domain;

/// <summary>
///     A single numbered provision of the Statute (CONTEXT.md). The id is the point's stable
///     identity across versions — renumbering or rewording in an amendment never changes it, so a
///     Report citing the point stays interpretable (ADR 0006). Only points flagged reportable may
///     be cited by a Report: the conduct rules are; the purpose and consequences framing never is.
/// </summary>
public sealed record StatutePoint(Guid Id, int Number, string Text, bool IsReportable);
