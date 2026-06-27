namespace ExamShield.Api.Contracts;

public sealed record ScoreCaptureRequest(Guid CaptureId);

public sealed record ScoreCaptureResponse(
    Guid ScoreId, int CorrectAnswers, int TotalQuestions, double Percentage);

public sealed record PublishResultsRequest(Guid ExamId);

public sealed record PublishResultsResponse(int PublishedCount);

public sealed record ScoreResultItem(
    Guid ScoreId, Guid CaptureId, Guid ExamId, Guid StudentId,
    int CorrectAnswers, int TotalQuestions, double Percentage, DateTimeOffset ScoredAt);

public sealed record GetResultsResponse(IReadOnlyList<ScoreResultItem> Results);

public sealed record GetStatisticsResponse(
    int TotalPapersScored, double AveragePercentage, int HighestScore, int LowestScore);

public sealed record ScoringQueueItemResponse(
    Guid CaptureId, Guid ExamId, Guid OcrResultId,
    string OcrStatus, double OverallConfidence, DateTimeOffset CompletedAt);

public sealed record ScoringQueueResponse(IReadOnlyList<ScoringQueueItemResponse> Items);

public sealed record BatchScoreRequest(Guid ExamId);
public sealed record BatchScoreResponse(Guid ExamId, int Scored, int Skipped);
