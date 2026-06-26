using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetResults;

public sealed class GetResultsQueryHandler : IRequestHandler<GetResultsQuery, GetResultsResult>
{
    private readonly IScoreRepository _scores;
    private readonly ICacheService _cache;

    public GetResultsQueryHandler(IScoreRepository scores, ICacheService cache)
    {
        _scores = scores;
        _cache = cache;
    }

    public async Task<GetResultsResult> Handle(GetResultsQuery query, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<GetResultsResult>(CacheKeys.PublishedResults, ct);
        if (cached is not null) return cached;

        var scores = await _scores.GetPublishedAsync(ct);
        var result = new GetResultsResult(scores
            .Select(s => new ScoreDto(
                s.Id.Value, s.CaptureId.Value, s.ExamId.Value, s.StudentId.Value,
                s.CorrectAnswers, s.TotalQuestions, s.Percentage, s.ScoredAt))
            .ToList());

        await _cache.SetAsync(CacheKeys.PublishedResults, result, TimeSpan.FromMinutes(5), ct);
        return result;
    }
}
