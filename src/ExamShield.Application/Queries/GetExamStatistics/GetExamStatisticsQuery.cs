using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetExamStatistics;

public sealed record ExamStatisticsResult(
    Guid ExamId,
    int TotalStudents,
    double AveragePercentage,
    double HighestPercentage,
    double LowestPercentage,
    double PassRate,
    IReadOnlyDictionary<string, int> GradeDistribution);

public sealed record GetExamStatisticsQuery(Guid ExamId) : IRequest<ExamStatisticsResult>;

public sealed class GetExamStatisticsQueryHandler(IScoreRepository scores)
    : IRequestHandler<GetExamStatisticsQuery, ExamStatisticsResult>
{
    private const double PassThreshold = 60.0;

    public async Task<ExamStatisticsResult> Handle(GetExamStatisticsQuery request, CancellationToken ct)
    {
        var all = await scores.GetByExamIdAsync(new ExamId(request.ExamId), ct);

        if (all.Count == 0)
            return new ExamStatisticsResult(request.ExamId, 0, 0, 0, 0, 0,
                new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0, ["D"] = 0, ["F"] = 0 });

        var percentages = all.Select(s => s.Percentage).ToList();
        var avg     = percentages.Average();
        var highest = percentages.Max();
        var lowest  = percentages.Min();
        var passed  = percentages.Count(p => p >= PassThreshold);
        var passRate = Math.Round(passed / (double)all.Count * 100, 2);

        var grades = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["C"] = 0, ["D"] = 0, ["F"] = 0 };
        foreach (var p in percentages)
            grades[ToGrade(p)]++;

        return new ExamStatisticsResult(
            request.ExamId,
            all.Count,
            Math.Round(avg, 2),
            Math.Round(highest, 2),
            Math.Round(lowest, 2),
            passRate,
            grades);
    }

    private static string ToGrade(double pct) => pct switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _     => "F"
    };
}
