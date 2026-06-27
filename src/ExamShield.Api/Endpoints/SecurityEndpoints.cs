using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.GetAllActiveSessions;
using ExamShield.Application.Queries.GetLoginHistory;
using ExamShield.Application.Queries.GetSecurityEvents;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class SecurityEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/security/events", ListSecurityEventsAsync)
            .WithName("ListSecurityEvents")
            .WithTags("Security")
            .RequireAuthorization("Administrator")
            .Produces<SecurityEventListResponse>();

        app.MapGet("/security/login-history", GetLoginHistoryAsync)
            .WithName("GetLoginHistory")
            .WithTags("Security")
            .RequireAuthorization()
            .Produces<LoginHistoryResponse>();

        app.MapGet("/security/sessions", async (
            ISender sender, CancellationToken ct, Guid? userId = null) =>
        {
            var result = await sender.Send(new GetAllActiveSessionsQuery(userId), ct);
            var entries = result.Sessions
                .Select(s => new AllSessionEntry(s.Id, s.UserId, s.CreatedAt, s.ExpiresAt))
                .ToList();
            return Results.Ok(new AllSessionsResponse(entries));
        })
        .WithName("GetAllActiveSessions")
        .WithTags("Security")
        .RequireAuthorization("Administrator")
        .Produces<AllSessionsResponse>();

        return app;
    }

    private static async Task<IResult> ListSecurityEventsAsync(
        ISender sender, int limit = 100, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetSecurityEventsQuery(limit), ct);
        var response = new SecurityEventListResponse(
            result.Events.Select(e => new SecurityEventResponse(
                e.Id, e.EventType, e.Severity, e.Message,
                e.UserId, e.IpAddress, e.CaptureId, e.OccurredAt
            )).ToList());
        return Results.Ok(response);
    }

    private static async Task<IResult> GetLoginHistoryAsync(
        ISender sender, int limit = 100, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetLoginHistoryQuery(limit), ct);
        var response = new LoginHistoryResponse(
            result.Events.Select(e =>
                new LoginHistoryEntry(e.Id, e.EventType, e.UserId, e.IpAddress, e.OccurredAt))
            .ToList());
        return Results.Ok(response);
    }
}
