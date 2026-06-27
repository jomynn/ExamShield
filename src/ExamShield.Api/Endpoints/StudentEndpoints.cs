using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.SubmitReviewRequest;
using ExamShield.Application.Queries.GetReviewRequests;
using ExamShield.Application.Queries.GetStudentResults;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/student/results", async (Guid studentId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetStudentResultsQuery(studentId), ct);
            var items = result.Results
                .Select(r => new StudentResultItemResponse(
                    r.ScoreId, r.CaptureId, r.ExamId, r.ExamName,
                    r.CorrectAnswers, r.TotalQuestions, r.Percentage,
                    r.ScoredAt, r.HashHex, r.IsVerified))
                .ToList();
            return Results.Ok(new StudentResultsResponse(result.StudentId, items));
        })
        .WithName("GetStudentResults")
        .WithTags("Student")
        .RequireAuthorization("Operator")
        .Produces<StudentResultsResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/student/review-request",
            async (SubmitReviewRequestBody body, ISender sender, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Reason))
                    return Results.BadRequest(new { title = "Reason cannot be empty.", status = 400 });

                var result = await sender.Send(
                    new SubmitReviewRequestCommand(body.CaptureId, body.StudentId, body.Reason), ct);
                return Results.Created(
                    $"/student/review-requests/{result.ReviewRequestId}",
                    new SubmitReviewRequestResponse(result.ReviewRequestId));
            })
        .WithName("SubmitReviewRequest")
        .WithTags("Student")
        .RequireAuthorization("Operator")
        .Produces<SubmitReviewRequestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/student/review-requests",
            async (Guid studentId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetReviewRequestsQuery(studentId), ct);
                var items = result.Items
                    .Select(r => new ReviewRequestItemResponse(
                        r.ReviewRequestId, r.StudentId, r.CaptureId,
                        r.Reason, r.Status, r.CreatedAt))
                    .ToList();
                return Results.Ok(new ReviewRequestListResponse(items));
            })
        .WithName("GetReviewRequests")
        .WithTags("Student")
        .RequireAuthorization("Operator")
        .Produces<ReviewRequestListResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
