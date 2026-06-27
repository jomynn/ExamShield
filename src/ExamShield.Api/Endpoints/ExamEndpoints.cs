using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.ActivateExam;
using ExamShield.Application.Commands.CloseExam;
using ExamShield.Application.Commands.CreateExam;
using ExamShield.Application.Commands.EnrollStudent;
using ExamShield.Application.Commands.SetAnswerKey;
using ExamShield.Application.Queries.GetAnswerKey;
using ExamShield.Application.Queries.GetExamCandidates;
using ExamShield.Application.Queries.GetExamSubmissionStatus;
using ExamShield.Application.Queries.GetExams;
using ExamShield.Domain.Exceptions;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class ExamEndpoints
{
    public static IEndpointRouteBuilder MapExamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/exams").WithTags("Exams");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetExamsQuery(page, pageSize), ct);
            var items = result.Exams
                .Select(e => new ExamResponse(e.ExamId, e.Name, e.Description, e.Status, e.TotalQuestions, e.CreatedAt))
                .ToList();
            return Results.Ok(new ExamListResponse(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        })
        .WithName("GetExams")
        .RequireAuthorization("Operator")
        .Produces<ExamListResponse>();

        group.MapPost("/", async (CreateExamRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new CreateExamCommand(request.Name, request.Description, request.TotalQuestions), ct);
            var response = new ExamResponse(
                result.ExamId, result.Name, result.Description,
                result.Status, result.TotalQuestions, result.CreatedAt);
            return Results.Created($"/exams/{result.ExamId}", response);
        })
        .WithName("CreateExam")
        .RequireAuthorization("Administrator")
        .Produces<ExamResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}/activate", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new ActivateExamCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("ActivateExam")
        .RequireAuthorization("Administrator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}/close", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new CloseExamCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("CloseExam")
        .RequireAuthorization("Administrator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/answer-key", async (Guid id, SetAnswerKeyRequest request, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new SetAnswerKeyCommand(id, request.Answers), ct);
            return Results.NoContent();
        })
        .WithName("SetAnswerKey")
        .RequireAuthorization("Administrator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/answer-key", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAnswerKeyQuery(id), ct);
            var response = new AnswerKeyResponse(
                result.ExamId,
                result.Answers.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                result.CreatedAt);
            return Results.Ok(response);
        })
        .WithName("GetAnswerKey")
        .RequireAuthorization("Operator")
        .Produces<AnswerKeyResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/students", async (Guid id, EnrollStudentRequest request, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new EnrollStudentCommand(id, request.StudentId), ct);
            return Results.NoContent();
        })
        .WithName("EnrollStudent")
        .RequireAuthorization("Administrator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/students", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetExamCandidatesQuery(id), ct);
            var items = result.Candidates
                .Select(c => new ExamCandidateItem(c.StudentId, c.EnrolledAt))
                .ToList();
            return Results.Ok(new ExamCandidatesResponse(id, items));
        })
        .WithName("GetExamCandidates")
        .RequireAuthorization("Operator")
        .Produces<ExamCandidatesResponse>();

        group.MapGet("/{id:guid}/submission-status", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetExamSubmissionStatusQuery(id), ct);
            var students = result.Students
                .Select(s => new StudentSubmissionItem(s.StudentId, s.HasSubmitted, s.CaptureStatus))
                .ToList();
            return Results.Ok(new ExamSubmissionStatusResponse(
                result.ExamId, result.TotalEnrolled, result.Submitted, result.Missing, students));
        })
        .WithName("GetExamSubmissionStatus")
        .RequireAuthorization("Operator")
        .Produces<ExamSubmissionStatusResponse>();

        return app;
    }
}
