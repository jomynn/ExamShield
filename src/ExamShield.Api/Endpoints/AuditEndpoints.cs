using System.Text;
using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.ExportAuditLog;
using ExamShield.Application.Queries.GetAuditLog;
using ExamShield.Application.Queries.VerifyAuditChain;
using ExamShield.Domain.Enums;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit", GetAuditLogAsync)
            .WithName("GetAuditLog")
            .WithTags("Audit")
            .RequireAuthorization("Auditor")
            .Produces<AuditLogResponse>();

        app.MapGet("/audit/export", ExportAuditLogAsync)
            .WithName("ExportAuditLog")
            .WithTags("Audit")
            .RequireAuthorization("Auditor")
            .Produces<byte[]>();

        app.MapGet("/audit/verify/{captureId:guid}", VerifyAuditChainAsync)
            .WithName("VerifyAuditChain")
            .WithTags("Audit")
            .RequireAuthorization("SecurityOfficer")
            .Produces<VerifyAuditChainResponse>();

        return app;
    }

    private static async Task<IResult> GetAuditLogAsync(
        ISender sender,
        Guid? captureId = null,
        int page = 1,
        int pageSize = 50,
        string? action = null,
        string? userId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (action is not null && !Enum.TryParse<AuditAction>(action, ignoreCase: true, out _))
            return Results.BadRequest(new { error = $"Unknown audit action '{action}'." });

        var result = await sender.Send(
            new GetAuditLogQuery(captureId, page, pageSize, action, userId, from, to), ct);

        var response = new AuditLogResponse(
            result.Entries.Select(e => new AuditLogEntryResponse(
                e.Id, e.Action, e.CaptureId, e.UserId, e.IpAddress, e.OccurredAt, e.Reason,
                e.ContentHash, e.ServerSignature
            )).ToList(),
            result.TotalCount);

        return Results.Ok(response);
    }

    private static async Task<IResult> ExportAuditLogAsync(
        ISender sender,
        Guid? captureId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ExportAuditLogQuery(captureId, from, to), ct);
        var bytes = Encoding.UTF8.GetBytes(result.Csv);
        return Results.File(bytes, "text/csv", result.Filename);
    }

    private static async Task<IResult> VerifyAuditChainAsync(
        Guid captureId, ISender sender, CancellationToken ct = default)
    {
        var result = await sender.Send(new VerifyAuditChainQuery(captureId), ct);
        return Results.Ok(new VerifyAuditChainResponse(
            result.IsValid, result.EntryCount, result.FirstBrokenIndex));
    }
}
