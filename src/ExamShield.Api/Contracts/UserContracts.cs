namespace ExamShield.Api.Contracts;

public sealed record UserResponse(
    Guid UserId, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);

public sealed record UserListResponse(
    IReadOnlyList<UserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record UpdateUserRoleRequest(string Role);
