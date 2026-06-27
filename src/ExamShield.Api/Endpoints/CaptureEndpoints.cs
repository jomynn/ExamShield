using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Application.Commands.VerifyIntegrity;
using ExamShield.Application.Queries.ExportCaptures;
using ExamShield.Application.Queries.GetCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class CaptureEndpoints
{
    public static IEndpointRouteBuilder MapCaptureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/capture").WithTags("Capture");

        // GET /captures — list captures with optional examId/status filters
        app.MapGet("/captures", async (
            IMediator mediator, CancellationToken ct,
            int page = 1, int pageSize = 50,
            Guid? examId = null, string? status = null) =>
        {
            CaptureStatus? parsedStatus = null;
            if (status is not null)
            {
                if (!Enum.TryParse<CaptureStatus>(status, ignoreCase: true, out var s))
                    return Results.BadRequest(new { title = $"Unknown status '{status}'.", status = 400 });
                parsedStatus = s;
            }

            var result = await mediator.Send(new GetCapturesQuery(page, pageSize, examId, parsedStatus), ct);
            var items = result.Captures
                .Select(c => new CaptureListItem(
                    c.CaptureId, c.ExamId, c.StudentId, c.DeviceId,
                    c.Status, c.CapturedAt, c.StorageKey))
                .ToList();
            return Results.Ok(new CaptureListResponse(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        })
        .WithName("GetCaptures")
        .WithTags("Capture")
        .RequireAuthorization("Operator")
        .Produces<CaptureListResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // GET /captures/export — download filtered capture list as CSV
        app.MapGet("/captures/export", async (
            IMediator mediator, CancellationToken ct,
            Guid? examId = null, string? status = null) =>
        {
            CaptureStatus? parsedStatus = null;
            if (status is not null)
            {
                if (!Enum.TryParse<CaptureStatus>(status, ignoreCase: true, out var s))
                    return Results.BadRequest(new { title = $"Unknown status '{status}'.", status = 400 });
                parsedStatus = s;
            }

            var result = await mediator.Send(new ExportCapturesQuery(examId, parsedStatus), ct);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(result.Csv),
                "text/csv",
                result.Filename);
        })
        .WithName("ExportCaptures")
        .WithTags("Capture")
        .RequireAuthorization("Operator")
        .Produces<byte[]>(StatusCodes.Status200OK, "text/csv")
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", RegisterCaptureAsync)
            .WithName("RegisterCapture")
            .RequireAuthorization("Operator")
            .Produces<RegisterCaptureResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPost("/{id:guid}/verify", VerifyIntegrityAsync)
            .WithName("VerifyCapture")
            .RequireAuthorization("Operator")
            .Produces<VerifyIntegrityResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Also register the /captures/{id}/image route on the root app (not under /capture group)
        app.MapGet("/captures/{id:guid}/image", GetCaptureImageAsync)
            .WithName("GetCaptureImage")
            .WithTags("Capture")
            .RequireAuthorization("Operator")
            .Produces<byte[]>(StatusCodes.Status200OK, "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterCaptureAsync(
        RegisterCaptureRequest request, ISender sender, CancellationToken ct)
    {
        var command = new RegisterCaptureCommand(
            request.ExamId, request.StudentId, request.DeviceId,
            request.PageNumber, request.HashHex, request.SignatureBytes);

        var result = await sender.Send(command, ct);

        return Results.Created(
            $"/capture/{result.CaptureId}",
            new RegisterCaptureResponse(result.CaptureId));
    }

    private static async Task<IResult> VerifyIntegrityAsync(
        Guid id, VerifyIntegrityRequest request, ISender sender, CancellationToken ct)
    {
        var command = new VerifyIntegrityCommand(id, request.ImageBytes);
        var result = await sender.Send(command, ct);

        return Results.Ok(new VerifyIntegrityResponse(
            result.IsValid, result.ExpectedHash, result.ActualHash));
    }

    private static async Task<IResult> GetCaptureImageAsync(
        Guid id,
        ICaptureRepository captures,
        IImageStorage imageStorage,
        CancellationToken ct)
    {
        var capture = await captures.GetByIdAsync(new CaptureId(id), ct)
            ?? throw new CaptureNotFoundException(id);

        if (capture.StorageKey is null)
            return Results.NotFound("Image not yet uploaded.");

        var bytes = await imageStorage.RetrieveAsync(capture.StorageKey, ct);
        return Results.Bytes(bytes, "application/octet-stream");
    }
}
