using Podkop.Moderation.Application;
using Podkop.Moderation.Domain;

namespace Podkop.Moderation.Infrastructure;

public sealed class InMemoryVerdictRepository(IEnumerable<Verdict> verdicts) : IVerdictRepository
{
    private readonly List<Verdict> _verdicts = verdicts.ToList();

    public Task AddAsync(Verdict verdict, CancellationToken cancellationToken)
    {
        _verdicts.Add(verdict);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Verdict>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Verdict>>(_verdicts.ToList());

    public Task<IReadOnlyList<Verdict>> GetByTargetAsync(ReportTargetKind targetKind, Guid targetId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Verdict>>(_verdicts
            .Where(verdict => verdict.TargetKind == targetKind && verdict.TargetId == targetId)
            .ToList());

    public Task<IReadOnlyList<Verdict>> GetByTargetsAsync(ReportTargetKind targetKind,
        IReadOnlyList<Guid> targetIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Verdict>>(_verdicts
            .Where(verdict => verdict.TargetKind == targetKind && targetIds.Contains(verdict.TargetId))
            .ToList());
}
