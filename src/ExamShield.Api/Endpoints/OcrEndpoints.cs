using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.TriggerBatchOcr;
using ExamShield.Application.Messages;
using ExamShield.Application.Queries.GetOcrQueue;
using ExamShield.Application.Queries.GetOcrResult;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class OcrEndpoints
{
    public static void MapOcrEndpoints(this WebApplication app)
    {
        app.MapPost("/ocr/{captureId:guid}", async (
            Guid captureId,
            IMessagePublisher publisher,
            CancellationToken ct) =>
        {
            await publisher.PublishAsync(new OcrRequestedMessage(captureId), ct);
            return Results.Accepted();
        })
        .RequireAuthorization("Operator");

        app.MapPost("/ocr/batch", async (BatchOcrRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new TriggerBatchOcrCommand(request.ExamId), ct);
            return Results.Ok(new BatchOcrResponse(request.ExamId, result.Queued, result.Skipped));
        })
        .WithName("BatchOcr")
        .RequireAuthorization("Operator")
        .Produces<BatchOcrResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/ocr/queue", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOcrQueueQuery(), ct);
            var items = result.Items
                .Select(i => new OcrQueueItemResponse(i.CaptureId, i.ExamId, i.StudentId, i.UploadedAt))
                .ToList();
            return Results.Ok(new OcrQueueResponse(items));
        })
        .WithName("GetOcrQueue")
        .RequireAuthorization("Operator")
        .Produces<OcrQueueResponse>();

        app.MapGet("/ocr/{captureId:guid}", async (
            Guid captureId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOcrResultQuery(captureId), ct);
            return Results.Ok(new GetOcrResultResponse(
                result.OcrResultId, result.CaptureId, result.Status.ToString(),
                result.OverallConfidence, result.RequiresManualReview,
                result.Answers.Select(a => new OcrAnswerResponse(a.QuestionNumber, a.Text, a.Confidence)).ToList()));
        })
        .RequireAuthorization("Operator");
    }
}
