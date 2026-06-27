using System.Text;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.ExportCaptures;

public sealed class ExportCapturesQueryHandler(ICaptureRepository repository)
    : IRequestHandler<ExportCapturesQuery, ExportCapturesResult>
{
    private static readonly string[] CsvHeader =
        ["CaptureId", "ExamId", "StudentId", "DeviceId", "PageNumber", "Status", "CapturedAt", "StorageKey"];

    public async Task<ExportCapturesResult> Handle(ExportCapturesQuery query, CancellationToken ct)
    {
        IReadOnlyList<Capture> all = query.ExamId.HasValue
            ? await repository.ListByExamIdAsync(new ExamId(query.ExamId.Value), ct)
            : await repository.ListAllAsync(ct);

        var filtered = query.Status.HasValue
            ? all.Where(c => c.Status == query.Status.Value).ToList()
            : (IReadOnlyList<Capture>)all;

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", CsvHeader));

        foreach (var c in filtered)
        {
            csv.AppendLine(string.Join(",",
                c.Id.Value,
                c.ExamId.Value,
                c.StudentId.Value,
                c.DeviceId.Value,
                c.PageNumber.Value,
                c.Status,
                c.CapturedAt.ToString("O"),
                EscapeCsv(c.StorageKey ?? "")));
        }

        var filename = $"captures-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new ExportCapturesResult(csv.ToString(), filename);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
