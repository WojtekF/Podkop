using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Application;

/// <summary>
///     The verdict store (issue #35). Verdicts are the moderation slice's second aggregate:
///     reports stay immutable, and resolution is derived — a report is pending iff its id
///     appears in no verdict's <see cref="Verdict.ResolvedReportIds" />. The store only holds
///     and hands back verdicts; deriving pending-ness against them is the Application
///     handlers' job, and no promise is made about the order anything is returned in.
/// </summary>
public interface IVerdictRepository
{
    Task AddAsync(Verdict verdict, CancellationToken cancellationToken);

    /// <summary>Every stored verdict — the Moderation Log's whole feed (issue #35).</summary>
    Task<IReadOnlyList<Verdict>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     The verdicts issued against one target — the resolution history callers scope a
    ///     single target's pending-ness against (the dismiss command, the one-target
    ///     my-report and duplicate rules). A never-judged target yields none.
    /// </summary>
    Task<IReadOnlyList<Verdict>> GetByTargetAsync(
        ReportTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);

    /// <summary>
    ///     The verdicts issued against the named targets of one kind — the batch behind
    ///     pending-scoping many targets in one read (a discussion's my-reports, the queue).
    ///     Naming no targets yields no verdicts.
    /// </summary>
    Task<IReadOnlyList<Verdict>> GetByTargetsAsync(
        ReportTargetKind targetKind, IReadOnlyList<Guid> targetIds, CancellationToken cancellationToken);
}
