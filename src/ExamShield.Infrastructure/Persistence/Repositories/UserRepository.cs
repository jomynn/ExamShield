using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ExamShieldDbContext _context;

    public UserRepository(ExamShieldDbContext context) => _context = context;

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public Task<User?> FindByEmailAsync(Email email, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<User>> ListAllAsync(CancellationToken ct = default) =>
        await _context.Users.ToListAsync(ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize,
        string? search = null, string? role = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Value.Contains(search));
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role, out var parsedRole))
                return ([], 0);
            query = query.Where(u => u.Role == parsedRole);
        }
        if (isActive is not null)
            query = query.Where(u => u.IsActive == isActive);
        query = query.OrderBy(u => u.Email);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
