using InfinitoCoffee.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Repositories;

internal static class EfRepositorySaveChanges
{
    public static async Task SaveAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("A concurrency conflict occurred while saving changes.");
        }
    }
}
