using InfinitoCoffee.Domain.Branches;

namespace InfinitoCoffee.Application.Branches;

public interface IBranchContext { int BranchId { get; } }

public sealed class BranchContext : IBranchContext
{
    public int BranchId { get; set; } = Branch.DefaultId;
}

public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct);
    Task<Branch?> GetByIdAsync(int id, CancellationToken ct);
}
