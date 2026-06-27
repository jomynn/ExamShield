using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.BatchScore;
using ExamShield.Application.Commands.PublishResults;
using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Application.Queries.ExportScores;
using ExamShield.Application.Queries.GetResults;
using ExamShield.Application.Queries.GetScoringQueue;
using ExamShield.Application.Queries.GetExamRankings;
using ExamShield.Application.Queries.GetExamStatistics;
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

        app.MapGet("/score/export",
            async (Guid? examId, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new ExportScoresQuery(examId), ct);
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(result.Csv),
                    "text/csv",
                    result.Filename);
            })
        .WithName("ExportScores")
        .RequireAuthorization("Auditor")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        app.MapGet("/statistics", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetStatisticsQuery(), ct);
            return Results.Ok(new GetStatisticsResponse(
                result.TotalPapersScored, result.AveragePercentage,
                result.HighestScore, result.LowestScore));
        })
        .RequireAuthorization("Auditor");

        app.MapGet("/score/rankings/{examId:guid}", async (Guid examId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetExamRankingsQuery(examId), ct);
            var entries = result.Rankings
                .Select(r => new RankingEntryResponse(r.Rank, r.StudentId, r.CorrectAnswers, r.TotalQuestions, r.Percentage))
                .ToList();
            return Results.Ok(new ExamRankingsResponse(examId, entries));
        })
        .WithName("GetExamRankings")
        .WithTags("Score")
        .RequireAuthorization("Auditor")
        .Produces<ExamRankingsResponse>();

        app.MapGet("/score/exams/{examId:guid}/statistics", async (Guid examId, IMediator mediator, CancellationToken ct) =>
        {
            var r = await mediator.Send(new GetExamStatisticsQuery(examId), ct);
            return Results.Ok(new ExamStatisticsResponse(
                r.ExamId, r.TotalStudents, r.AveragePercentage,
                r.HighestPercentage, r.LowestPercentage, r.PassRate, r.GradeDistribution));
        })
        .WithName("GetExamStatistics")
        .WithTags("Score")
        .RequireAuthorization("Auditor")
        .Produces<ExamStatisticsResponse>();
    }
}
