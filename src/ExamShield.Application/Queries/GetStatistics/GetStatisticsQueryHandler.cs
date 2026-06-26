using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetStatistics;

public sealed class GetStatisticsQueryHandler : IRequestHandler<GetStatisticsQuery, GetStatisticsResult>
{
    private readonly IScoreRepository _scores;
    private readonly ICacheService _cache;

    public GetStatisticsQueryHandler(IScoreRepository scores, ICacheService cache)
    {
        _scores = scores;
        _cache = cache;
    }

    public async Task<GetStatisticsResult> Handle(GetStatisticsQuery query, CancellationToken ct)
    {
        var cached = await _cache.GetAsync<GetStatisticsResult>(CacheKeys.Statistics, ct);
        if (cached is not null) return cached;

        var scores = await _scores.GetAllAsync(ct);

        var result = scores.Count == 0
            ? new GetStatisticsResult(0, 0.0, 0, 0)
            : new GetStatisticsResult(
                scores.Count,
                Math.Round(scores.Average(s => s.Percentage), 2),
                scores.Max(s => s.CorrectAnswers),
                scores.Min(s => s.CorrectAnswers));

        await _cache.SetAsync(CacheKeys.Statistics, result, TimeSpan.FromMinutes(2), ct);
        return result;
    }
}
