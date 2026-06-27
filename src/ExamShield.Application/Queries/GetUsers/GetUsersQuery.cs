using MediatR;

namespace ExamShield.Application.Queries.GetUsers;

public sealed record UserDto(
    Guid UserId, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);

public sealed record GetUsersResult(
    IReadOnlyList<UserDto> Users,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed record GetUsersQuery(
    int Page = 1, int PageSize = 50,
    string? Search = null, string? Role = null,
    bool? IsActive = null)
    : IRequest<GetUsersResult>;
