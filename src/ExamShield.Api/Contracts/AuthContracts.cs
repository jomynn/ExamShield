namespace ExamShield.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, string RefreshToken, string Role, bool RequiresMfa = false);
public sealed record RefreshRequest(string RefreshToken);
public sealed record MfaLoginRequest(string Email, string Password, string Code);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ProfileResponse(string Email, string Role, bool MfaEnabled);
public sealed record SessionResponse(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record AllSessionEntry(Guid Id, Guid UserId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public sealed record AllSessionsResponse(IReadOnlyList<AllSessionEntry> Sessions);
public sealed record SessionsResponse(IReadOnlyList<SessionResponse> Sessions);

public sealed record CreateUserRequest(string Email, string Password, string Role);

public sealed record CreateUserResponse(Guid UserId);
