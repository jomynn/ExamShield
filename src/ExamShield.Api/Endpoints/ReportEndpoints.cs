using System.Text;
using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.GetExamReport;
using ExamShield.Application.Queries.GetReportSummary;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reports/summary", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetReportSummaryQuery(), ct);
            return Results.Ok(new ReportSummaryResponse(
                result.GeneratedAt,
                new CaptureStatsResponse(
                    result.Captures.Total, result.Captures.Created,
                    result.Captures.Uploaded, result.Captures.Verified, result.Captures.Tampered),
                new OcrStatsResponse(result.Ocr.TotalProcessed, result.Ocr.AverageConfidence),
                new ScoreStatsResponse(
                    result.Scores.TotalScored, result.Scores.AveragePercentage,
                    result.Scores.HighestPercentage, result.Scores.LowestPercentage),
                new SecurityStatsResponse(result.Security.TotalEvents, result.Security.CriticalEvents)));
        })
        .WithName("GetReportSummary")
        .WithTags("Reports")
        .RequireAuthorization("Operator")
        .Produces<ReportSummaryResponse>();

        app.MapGet("/reports/exam/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetExamReportQuery(id), ct);
            return Results.Ok(new ExamReportResponse(
                result.ExamId, result.ExamName, result.ExamStatus, result.TotalQuestions,
                result.GeneratedAt, result.TotalCaptures, result.UploadedCaptures,
                result.VerifiedCaptures, result.TamperedCaptures, result.TotalOcrProcessed,
                result.OcrAverageConfidence, result.LowConfidenceCount, result.TotalScored,
                result.AverageScorePercentage, result.HighestScorePercentage,
                result.LowestScorePercentage, result.TotalReviewRequests));
        })
        .WithName("GetExamReport")
        .WithTags("Reports")
        .RequireAuthorization("Operator")
        .Produces<ExamReportResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/reports/exam/{id:guid}/export", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var r = await sender.Send(new GetExamReportQuery(id), ct);
            var csv = new StringBuilder();
            csv.AppendLine("Field,Value");
            csv.AppendLine($"ExamId,{r.ExamId}");
            csv.AppendLine($"ExamName,\"{r.ExamName}\"");
            csv.AppendLine($"Status,{r.ExamStatus}");
            csv.AppendLine($"TotalQuestions,{r.TotalQuestions}");
            csv.AppendLine($"GeneratedAt,{r.GeneratedAt:O}");
            csv.AppendLine($"TotalCaptures,{r.TotalCaptures}");
            csv.AppendLine($"UploadedCaptures,{r.UploadedCaptures}");
            csv.AppendLine($"VerifiedCaptures,{r.VerifiedCaptures}");
            csv.AppendLine($"TamperedCaptures,{r.TamperedCaptures}");
            csv.AppendLine($"TotalOcrProcessed,{r.TotalOcrProcessed}");
            csv.AppendLine($"OcrAverageConfidence,{r.OcrAverageConfidence:F4}");
            csv.AppendLine($"LowConfidenceCount,{r.LowConfidenceCount}");
            csv.AppendLine($"TotalScored,{r.TotalScored}");
            csv.AppendLine($"AverageScorePercentage,{r.AverageScorePercentage:F2}");
            csv.AppendLine($"HighestScorePercentage,{r.HighestScorePercentage:F2}");
            csv.AppendLine($"LowestScorePercentage,{r.LowestScorePercentage:F2}");
            csv.AppendLine($"TotalReviewRequests,{r.TotalReviewRequests}");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return Results.File(bytes, "text/csv", $"exam-report-{r.ExamId}.csv");
        })
        .WithName("ExportExamReport")
        .WithTags("Reports")
        .RequireAuthorization("Operator")
        .Produces<byte[]>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
