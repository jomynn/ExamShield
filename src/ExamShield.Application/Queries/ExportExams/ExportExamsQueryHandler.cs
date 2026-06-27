using System.Text;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.ExportExams;

public sealed record ExportExamsResult(string Csv, string Filename);

public sealed record ExportExamsQuery(
    string? Search = null,
    ExamStatus? Status = null)
    : IRequest<ExportExamsResult>;

public sealed class ExportExamsQueryHandler(IExamRepository exams)
    : IRequestHandler<ExportExamsQuery, ExportExamsResult>
{
    private static readonly string[] Header =
        ["ExamId", "Name", "Description", "Status", "TotalQuestions", "CreatedAt"];

    public async Task<ExportExamsResult> Handle(ExportExamsQuery query, CancellationToken ct)
    {
        var all = await exams.ListAllAsync(ct);

        IEnumerable<Exam> filtered = all;
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(e => e.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        if (query.Status is not null)
            filtered = filtered.Where(e => e.Status == query.Status);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", Header));

        foreach (var e in filtered.OrderByDescending(x => x.CreatedAt))
        {
            csv.AppendLine(string.Join(",",
                e.Id.Value,
                EscapeCsv(e.Name),
                EscapeCsv(e.Description ?? ""),
                e.Status.ToString(),
                e.TotalQuestions,
                e.CreatedAt.ToString("O")));
        }

        var filename = $"exams-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new ExportExamsResult(csv.ToString(), filename);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
