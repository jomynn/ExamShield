using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.ActivateExam;
using ExamShield.Application.Commands.CloseExam;
using ExamShield.Application.Commands.CreateExam;
using ExamShield.Application.Commands.BulkEnrollStudents;
using ExamShield.Application.Commands.EnrollStudent;
using ExamShield.Application.Commands.UpdateExam;
using ExamShield.Application.Commands.UnenrollStudent;
using ExamShield.Application.Commands.SetAnswerKey;
using ExamShield.Application.Queries.GetAnswerKey;
using ExamShield.Application.Queries.GetExamCandidates;
using ExamShield.Application.Queries.GetExamSubmissionStatus;
using ExamShield.Application.Queries.ExportExams;
using ExamShield.Application.Queries.GetExamById;
using ExamShield.Application.Queries.GetExams;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class ExamEndpoints
{
    public static IEndpointRouteBuilder MapExamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/exams").WithTags("Exams");

        group.MapGet("/", async (
            IMediator mediator, CancellationToken ct,
            int page = 1, int pageSize = 50,
            string? search = null, ExamStatus? status = null) =>
        {
            var result = await mediator.Send(new GetExamsQuery(page, pageSize, search, status), ct);
            var items = result.Exams
                .Select(e => new ExamResponse(e.ExamId, e.Name, e.Description, e.Status, e.TotalQuestions, e.CreatedAt, e.ScheduledAt, e.EndsAt))
                .ToList();
            return Results.Ok(new ExamListResponse(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        })
        .WithName("GetExams")
        .RequireAuthorization("Operator")
        .Produces<ExamListResponse>();

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetExamByIdQuery(id), ct);
            if (result is null) return Results.NotFound();
            return Results.Ok(new ExamResponse(
                result.ExamId, result.Name, result.Description,
                result.Status, result.TotalQuestions, result.CreatedAt,
                result.ScheduledAt, result.EndsAt));
        })
        .WithName("GetExamById")
        .RequireAuthorization("Operator")
        .Produces<ExamResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateExamRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new CreateExamCommand(request.Name, request.Description, request.TotalQuestions, request.ScheduledAt, request.EndsAt), ct);
            var response = new ExamResponse(
                result.ExamId, result.Name, result.Description,
                result.Status, result.TotalQuestions, result.CreatedAt,
                result.ScheduledAt, result.EndsAt);
            return Results.Created($"/exams/{result.ExamId}", response);
        })
        .WithName("CreateExam")
        .RequireAuthorization("Administrator")
        .Produces<ExamResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateExamRequest request, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                await mediator.Send(
                    new UpdateExamCommand(id, request.Name, request.Description,
                        request.ScheduledAt, request.EndsAt), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException e)
            {
                return Results.UnprocessableEntity(new { error = e.Message });
            }
        })
        .WithName("UpdateExam")
        .RequireAuthorization("Administrator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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

        group.MapPost("/{id:guid}/students/bulk",
            async (Guid id, BulkEnrollRequest request, IMediator mediator, CancellationToken ct) =>
            {
                if (request.StudentIds.Count == 0)
                    return Results.BadRequest(new { error = "StudentIds must not be empty." });

                var result = await mediator.Send(
                    new BulkEnrollStudentsCommand(id, request.StudentIds), ct);
                return Results.Ok(new BulkEnrollResponse(result.Enrolled, result.AlreadyEnrolled, result.Total));
            })
        .WithName("BulkEnrollStudents")
        .RequireAuthorization("Administrator")
        .Produces<BulkEnrollResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

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

        group.MapDelete("/{id:guid}/students/{studentId:guid}",
            async (Guid id, Guid studentId, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new UnenrollStudentCommand(id, studentId), ct);
                return Results.NoContent();
            })
        .WithName("UnenrollStudent")
        .RequireAuthorization("Operator")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

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

        group.MapGet("/export", async (
            IMediator mediator, CancellationToken ct,
            string? search = null, ExamStatus? status = null) =>
        {
            var result = await mediator.Send(new ExportExamsQuery(search, status), ct);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(result.Csv),
                "text/csv",
                result.Filename);
        })
        .WithName("ExportExams")
        .RequireAuthorization("Auditor")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        return app;
    }
}
