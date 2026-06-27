using ExamShield.Domain.Enums;

namespace ExamShield.Api.Contracts;

public sealed record TriggerOcrResponse(Guid OcrResultId, string Status, bool RequiresManualReview);

public sealed record BatchOcrRequest(Guid ExamId);
public sealed record BatchOcrResponse(Guid ExamId, int Queued, int Skipped);

public sealed record OcrQueueItemResponse(Guid CaptureId, Guid ExamId, Guid StudentId, DateTimeOffset UploadedAt);
public sealed record OcrQueueResponse(IReadOnlyList<OcrQueueItemResponse> Items);

public sealed record OcrAnswerResponse(int QuestionNumber, string Text, double Confidence);

public sealed record GetOcrResultResponse(
    Guid OcrResultId,
    Guid CaptureId,
    string Status,
    double OverallConfidence,
    bool RequiresManualReview,
    IReadOnlyList<OcrAnswerResponse> Answers);
