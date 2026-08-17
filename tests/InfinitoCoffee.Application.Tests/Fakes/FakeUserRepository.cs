using InfinitoCoffee.Application.Users.Contracts;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public int SaveChangesCalls { get; private set; }

    public Exception? SaveChangesException { get; set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public IReadOnlyCollection<User> Users => _users;

    public void Seed(params User[] users)
    {
        _users.AddRange(users);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_users.SingleOrDefault(user => user.Id == id));
    }

    public Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        return Task.FromResult<IReadOnlyCollection<User>>(_users.ToArray());
    }

    public Task<User?> GetByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        return Task.FromResult(
            _users.SingleOrDefault(user => user.NormalizedUsername == normalizedUsername));
    }

    public Task<bool> ExistsByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        return Task.FromResult(
            _users.Any(user => user.NormalizedUsername == normalizedUsername));
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastCancellationToken = cancellationToken;
        SaveChangesCalls++;

        return SaveChangesException is null
            ? Task.CompletedTask
            : Task.FromException(SaveChangesException);
    }
}
