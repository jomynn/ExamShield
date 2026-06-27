using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetChainOfCustody;

public sealed record ChainAuditEntry(string Action, string UserId, DateTimeOffset OccurredAt, string? Reason);

public sealed record ChainOcrInfo(
    Guid OcrResultId, double OverallConfidence, int AnswerCount, DateTimeOffset CompletedAt);

public sealed record ChainScoreInfo(
    Guid ScoreId, int CorrectAnswers, int TotalQuestions, double Percentage, DateTimeOffset ScoredAt);

public sealed record ChainReviewInfo(Guid ReviewId, string Status, DateTimeOffset RequestedAt);

public sealed record GetChainOfCustodyResult(
    Guid CaptureId, Guid ExamId, Guid StudentId, Guid DeviceId,
    int PageNumber, string HashHex, string Status,
    DateTimeOffset CapturedAt, string? StorageKey,
    ChainOcrInfo? OcrResult,
    ChainScoreInfo? Score,
    IReadOnlyList<ChainReviewInfo> Reviews,
    IReadOnlyList<ChainAuditEntry> AuditTrail);

public sealed record GetChainOfCustodyQuery(Guid CaptureId) : IRequest<GetChainOfCustodyResult>;

public sealed class GetChainOfCustodyQueryHandler(
    ICaptureRepository captures,
    IOcrResultRepository ocrResults,
    IScoreRepository scores,
    IReviewRequestRepository reviews,
    IAuditLogRepository audit)
    : IRequestHandler<GetChainOfCustodyQuery, GetChainOfCustodyResult>
{
    public async Task<GetChainOfCustodyResult> Handle(
        GetChainOfCustodyQuery query, CancellationToken ct)
    {
        var captureId = new CaptureId(query.CaptureId);

        var capture = await captures.GetByIdAsync(captureId, ct)
            ?? throw new CaptureNotFoundException(query.CaptureId);

        var ocrResult = await ocrResults.GetByCaptureIdAsync(captureId, ct);
        var score     = await scores.GetByCaptureIdAsync(captureId, ct);
        var captureReviews = await reviews.ListByCaptureIdsAsync(
            new[] { captureId }.ToList(), ct);
        var auditChain = await audit.GetChainAsync(captureId, ct);

        return new GetChainOfCustodyResult(
            CaptureId:  capture.Id.Value,
            ExamId:     capture.ExamId.Value,
            StudentId:  capture.StudentId.Value,
            DeviceId:   capture.DeviceId.Value,
            PageNumber: capture.PageNumber.Value,
            HashHex:    capture.ExpectedHash.Hex,
            Status:     capture.Status.ToString(),
            CapturedAt: capture.CapturedAt,
            StorageKey: capture.StorageKey,
            OcrResult:  ocrResult is null ? null : new ChainOcrInfo(
                ocrResult.Id.Value,
                ocrResult.OverallConfidence.Value,
                ocrResult.Answers.Count,
                ocrResult.ProcessedAt),
            Score: score is null ? null : new ChainScoreInfo(
                score.Id.Value,
                score.CorrectAnswers,
                score.TotalQuestions,
                score.Percentage,
                score.ScoredAt),
            Reviews: captureReviews
                .Select(r => new ChainReviewInfo(r.Id.Value, r.Status.ToString(), r.CreatedAt))
                .ToList(),
            AuditTrail: auditChain
                .Select(a => new ChainAuditEntry(a.Action.ToString(), a.UserId, a.OccurredAt, a.Reason))
                .ToList());
    }
}
