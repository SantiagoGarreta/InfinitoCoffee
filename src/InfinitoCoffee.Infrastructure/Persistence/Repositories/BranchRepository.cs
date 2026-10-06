using InfinitoCoffee.Application.Branches;
using InfinitoCoffee.Domain.Branches;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository(InfinitoCoffeeDbContext db) : IBranchRepository
{
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct) =>
        await db.Branches.OrderBy(x => x.Id).ToArrayAsync(ct);
    public Task<Branch?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Branches.SingleOrDefaultAsync(x => x.Id == id, ct);
}
