using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public sealed class OcrResult : AggregateRoot
{
    public OcrResultId Id { get; private set; } = null!;
    public CaptureId CaptureId { get; private set; } = null!;
    public IReadOnlyList<ExtractedAnswer> Answers { get; private set; } = [];
    public OcrConfidence OverallConfidence { get; private set; } = null!;
    public OcrStatus Status { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public bool RequiresManualReview => Status == OcrStatus.LowConfidence;

    private OcrResult() { }

    public static OcrResult Create(
        CaptureId captureId,
        IReadOnlyList<ExtractedAnswer> answers,
        double confidenceThreshold = OcrConfidence.LowThreshold)
    {
        ArgumentNullException.ThrowIfNull(captureId);
        ArgumentNullException.ThrowIfNull(answers);

        var overall = answers.Count > 0
            ? new OcrConfidence(answers.Average(a => a.Confidence.Value))
            : new OcrConfidence(0.0);

        var status = answers.Any(a => a.Confidence.Value < confidenceThreshold)
            ? OcrStatus.LowConfidence
            : OcrStatus.Completed;

        return new OcrResult
        {
            Id = OcrResultId.New(),
            CaptureId = captureId,
            Answers = answers.ToList(),
            OverallConfidence = overall,
            Status = status,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }
}
