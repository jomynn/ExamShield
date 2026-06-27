using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetExamCandidates;

public sealed record ExamCandidateDto(Guid StudentId, DateTimeOffset EnrolledAt);
public sealed record GetExamCandidatesResult(Guid ExamId, IReadOnlyList<ExamCandidateDto> Candidates);
public sealed record GetExamCandidatesQuery(Guid ExamId) : IRequest<GetExamCandidatesResult>;

public sealed class GetExamCandidatesQueryHandler(IExamCandidateRepository repository)
    : IRequestHandler<GetExamCandidatesQuery, GetExamCandidatesResult>
{
    public async Task<GetExamCandidatesResult> Handle(GetExamCandidatesQuery query, CancellationToken ct)
    {
        var list = await repository.ListByExamIdAsync(new ExamId(query.ExamId), ct);
        var dtos = list.Select(c => new ExamCandidateDto(c.StudentId.Value, c.EnrolledAt)).ToList();
        return new GetExamCandidatesResult(query.ExamId, dtos);
    }
}
