using ExamShield.Api.Contracts;
using ExamShield.Api.RateLimiting;
using ExamShield.Application.Commands.ChangePassword;
using ExamShield.Application.Commands.CreateUser;
using ExamShield.Application.Commands.ForgotPassword;
using ExamShield.Application.Commands.Login;
using ExamShield.Application.Commands.Logout;
using ExamShield.Application.Commands.MfaLogin;
using ExamShield.Application.Commands.Refresh;
using ExamShield.Application.Commands.ResetPassword;
using ExamShield.Application.Commands.RevokeAllSessions;
using ExamShield.Application.Commands.RevokeSession;
using ExamShield.Application.Queries.GetProfile;
using ExamShield.Application.Queries.ListActiveSessions;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using System.Security.Claims;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken")
            .AllowAnonymous()
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/mfa/login", MfaLoginAsync)
            .WithName("MfaLogin")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/change", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/forgot", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/password/reset", ResetPasswordAsync)
            .WithName("ResetPassword")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/profile", GetProfileAsync)
            .WithName("GetProfile")
            .RequireAuthorization()
            .Produces<ProfileResponse>();

        group.MapGet("/sessions", GetSessionsAsync)
            .WithName("GetSessions")
            .RequireAuthorization()
            .Produces<SessionsResponse>();

        group.MapDelete("/sessions/{sessionId:guid}", RevokeSessionAsync)
            .WithName("RevokeSession")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/sessions", RevokeAllSessionsAsync)
            .WithName("RevokeAllSessions")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/users", CreateUserAsync)
            .WithName("CreateUser")
            .RequireAuthorization("Administrator")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, ISender sender, HttpContext ctx, CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var result = await sender.Send(new LoginCommand(request.Email, request.Password, ip), ct);
        return Results.Ok(new LoginResponse(result.Token, result.RefreshToken, result.Role, result.RequiresMfa));
    }

    private static async Task<IResult> MfaLoginAsync(
        MfaLoginRequest request, ISender sender, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(
                new MfaLoginCommand(request.Email, request.Password, request.Code), ct);
            return Results.Ok(new LoginResponse(result.Token, result.RefreshToken, result.Role));
        }
        catch (Exception e) when (e is InvalidCredentialsException or UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request, ISender sender, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), ct);
            return Results.Ok(new LoginResponse(result.Token, result.RefreshToken, result.Role));
        }
        catch (InvalidCredentialsException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest request, ISender sender, CancellationToken ct)
    {
        await sender.Send(new LogoutCommand(request.RefreshToken), ct);
        return Results.NoContent();
    }

    private static Guid? ExtractUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? ctx.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request, ISender sender, HttpContext ctx, CancellationToken ct)
    {
        if (ExtractUserId(ctx) is not Guid userId) return Results.Unauthorized();
        try
        {
            await sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
            return Results.NoContent();
        }
        catch (InvalidCredentialsException)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> GetProfileAsync(
        ISender sender, HttpContext ctx, CancellationToken ct)
    {
        if (ExtractUserId(ctx) is not Guid userId) return Results.Unauthorized();
        var result = await sender.Send(new GetProfileQuery(userId), ct);
        return Results.Ok(new ProfileResponse(result.Email, result.Role, result.MfaEnabled));
    }

    private static async Task<IResult> GetSessionsAsync(
        ISender sender, HttpContext ctx, CancellationToken ct)
    {
        if (ExtractUserId(ctx) is not Guid userId) return Results.Unauthorized();
        var result = await sender.Send(new ListActiveSessionsQuery(userId), ct);
        var sessions = result.Sessions.Select(s => new SessionResponse(s.Id, s.CreatedAt, s.ExpiresAt)).ToList();
        return Results.Ok(new SessionsResponse(sessions));
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid sessionId, ISender sender, HttpContext ctx, CancellationToken ct)
    {
        if (ExtractUserId(ctx) is not Guid userId) return Results.Unauthorized();
        try
        {
            await sender.Send(new RevokeSessionCommand(userId, sessionId), ct);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> RevokeAllSessionsAsync(
        ISender sender, HttpContext ctx, CancellationToken ct)
    {
        if (ExtractUserId(ctx) is not Guid userId) return Results.Unauthorized();
        await sender.Send(new RevokeAllSessionsCommand(userId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request, ISender sender, CancellationToken ct)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            throw new ArgumentException($"Invalid role '{request.Role}'.", nameof(request));

        var result = await sender.Send(new CreateUserCommand(request.Email, request.Password, role), ct);
        return Results.Created($"/auth/users/{result.UserId}", new CreateUserResponse(result.UserId));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, ISender sender, CancellationToken ct)
    {
        await sender.Send(new ForgotPasswordCommand(request.Email), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, ISender sender, CancellationToken ct)
    {
        try
        {
            await sender.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
            return Results.NoContent();
        }
        catch (InvalidResetTokenException)
        {
            return Results.BadRequest(new { title = "Invalid or expired reset token.", status = 400 });
        }
    }
}
