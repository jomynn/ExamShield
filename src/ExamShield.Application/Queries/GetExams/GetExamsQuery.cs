using ExamShield.Domain.Entities;
using MediatR;

namespace ExamShield.Application.Queries.GetExams;

public sealed record ExamDto(
    Guid ExamId, string Name, string? Description,
    string Status, int TotalQuestions, DateTimeOffset CreatedAt);

public sealed record GetExamsResult(
    IReadOnlyList<ExamDto> Exams,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed record GetExamsQuery(
    int Page = 1, int PageSize = 50,
    string? Search = null, ExamStatus? Status = null)
    : IRequest<GetExamsResult>;
