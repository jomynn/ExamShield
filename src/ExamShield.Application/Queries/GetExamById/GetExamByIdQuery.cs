using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetExamById;

public sealed record GetExamByIdResult(
    Guid ExamId, string Name, string? Description,
    string Status, int TotalQuestions, DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt, DateTimeOffset? EndsAt);

public sealed record GetExamByIdQuery(Guid ExamId) : IRequest<GetExamByIdResult?>;

public sealed class GetExamByIdQueryHandler(IExamRepository exams)
    : IRequestHandler<GetExamByIdQuery, GetExamByIdResult?>
{
    public async Task<GetExamByIdResult?> Handle(GetExamByIdQuery query, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(query.ExamId), ct);
        if (exam is null) return null;

        return new GetExamByIdResult(
            exam.Id.Value, exam.Name, exam.Description,
            exam.Status.ToString(), exam.TotalQuestions, exam.CreatedAt,
            exam.ScheduledAt, exam.EndsAt);
    }
}
