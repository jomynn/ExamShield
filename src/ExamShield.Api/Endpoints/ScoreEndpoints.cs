using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.BatchScore;
using ExamShield.Application.Commands.PublishResults;
using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Application.Queries.GetResults;
using ExamShield.Application.Queries.GetScoringQueue;
using ExamShield.Application.Queries.GetStatistics;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class ScoreEndpoints
{
    public static void MapScoreEndpoints(this WebApplication app)
    {
        app.MapGet("/score/queue", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetScoringQueueQuery(), ct);
            var items = result.Items
                .Select(i => new ScoringQueueItemResponse(
                    i.CaptureId, i.ExamId, i.OcrResultId,
                    i.OcrStatus, i.OverallConfidence, i.CompletedAt))
                .ToList();
            return Results.Ok(new ScoringQueueResponse(items));
        })
        .WithName("GetScoringQueue")
        .RequireAuthorization("Operator")
        .Produces<ScoringQueueResponse>();

        app.MapPost("/score", async (ScoreCaptureRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ScoreCaptureCommand(request.CaptureId), ct);
            return Results.Ok(new ScoreCaptureResponse(
                result.ScoreId, result.CorrectAnswers, result.TotalQuestions, result.Percentage));
        })
        .RequireAuthorization("Operator");

        app.MapPost("/score/batch", async (BatchScoreRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new BatchScoreCommand(request.ExamId), ct);
            return Results.Ok(new BatchScoreResponse(request.ExamId, result.Scored, result.Skipped));
        })
        .WithName("BatchScore")
        .RequireAuthorization("Operator")
        .Produces<BatchScoreResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/results/publish",
            async (PublishResultsRequest request, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new PublishResultsCommand(request.ExamId), ct);
                return Results.Ok(new PublishResultsResponse(result.PublishedCount));
            })
        .RequireAuthorization("Administrator");

        app.MapGet("/results", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetResultsQuery(), ct);
            var items = result.Results.Select(s => new ScoreResultItem(
                s.ScoreId, s.CaptureId, s.ExamId, s.StudentId,
                s.CorrectAnswers, s.TotalQuestions, s.Percentage, s.ScoredAt)).ToList();
            return Results.Ok(new GetResultsResponse(items));
        })
        .RequireAuthorization("Operator");

        app.MapGet("/statistics", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetStatisticsQuery(), ct);
            return Results.Ok(new GetStatisticsResponse(
                result.TotalPapersScored, result.AveragePercentage,
                result.HighestScore, result.LowestScore));
        })
        .RequireAuthorization("Auditor");
    }
}
