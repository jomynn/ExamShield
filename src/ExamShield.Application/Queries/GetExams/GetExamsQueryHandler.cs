using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetExams;

public sealed class GetExamsQueryHandler(IExamRepository exams)
    : IRequestHandler<GetExamsQuery, GetExamsResult>
{
    public async Task<GetExamsResult> Handle(GetExamsQuery request, CancellationToken ct)
    {
        var (items, total) = await exams.ListPagedAsync(request.Page, request.PageSize, request.Search, request.Status, ct);
        var dtos = items
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExamDto(e.Id.Value, e.Name, e.Description, e.Status.ToString(), e.TotalQuestions, e.CreatedAt))
            .ToList();
        return new GetExamsResult(dtos, total, request.Page, request.PageSize);
    }
}
