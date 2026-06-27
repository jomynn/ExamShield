using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<UserId, User> _byId = new();

    public InMemoryUserRepository(IEnumerable<User>? seed = null)
    {
        foreach (var user in seed ?? [])
        {
            _byEmail[user.Email.Value] = user;
            _byId[user.Id] = user;
        }
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        if (!_byEmail.TryAdd(user.Email.Value, user))
            throw new InvalidOperationException($"User '{user.Email.Value}' already exists.");
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SaveAsync(User user, CancellationToken ct = default)
    {
        _byEmail[user.Email.Value] = user;
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<User?> FindByEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(_byEmail.TryGetValue(email.Value, out var user) ? user : null);

    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        Task.FromResult(_byId.TryGetValue(id, out var user) ? user : null);

    public Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<User>>(_byEmail.Values.ToList());

    public Task<(IReadOnlyList<User> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        string? search = null, string? role = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _byEmail.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Value.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role, out var parsedRole))
                return Task.FromResult<(IReadOnlyList<User>, int)>(([], 0));
            query = query.Where(u => u.Role == parsedRole);
        }
        if (isActive is not null)
            query = query.Where(u => u.IsActive == isActive);
        var all = query.OrderBy(u => u.Email.Value).ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<User>, int)>((items, all.Count));
    }
}
