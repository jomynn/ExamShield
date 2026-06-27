using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetUsers;

public sealed class GetUsersQueryHandler(IUserRepository users)
    : IRequestHandler<GetUsersQuery, GetUsersResult>
{
    public async Task<GetUsersResult> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var (items, total) = await users.ListPagedAsync(
            request.Page, request.PageSize, request.Search, request.Role, request.IsActive, ct);
        var dtos = items
            .OrderBy(u => u.Email.Value)
            .Select(u => new UserDto(u.Id.Value, u.Email.Value, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToList();
        return new GetUsersResult(dtos, total, request.Page, request.PageSize);
    }
}
