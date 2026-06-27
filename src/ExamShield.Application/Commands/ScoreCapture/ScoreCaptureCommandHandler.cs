using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.ScoreCapture;

public sealed class ScoreCaptureCommandHandler : IRequestHandler<ScoreCaptureCommand, ScoreCaptureResult>
{
    private readonly ICaptureRepository _captures;
    private readonly IOcrResultRepository _ocrResults;
    private readonly IAnswerKeyRepository _answerKeys;
    private readonly IScoreRepository _scores;
    private readonly IAuditLogRepository _auditLog;
    private readonly ICacheService _cache;

    public ScoreCaptureCommandHandler(
        ICaptureRepository captures, IOcrResultRepository ocrResults,
        IAnswerKeyRepository answerKeys, IScoreRepository scores,
        IAuditLogRepository auditLog, ICacheService cache)
    {
        _captures = captures;
        _ocrResults = ocrResults;
        _answerKeys = answerKeys;
        _scores = scores;
        _auditLog = auditLog;
        _cache = cache;
    }

    public async Task<ScoreCaptureResult> Handle(ScoreCaptureCommand command, CancellationToken ct)
    {
        var capture = await _captures.GetByIdAsync(new CaptureId(command.CaptureId), ct)
            ?? throw new CaptureNotFoundException(command.CaptureId);

        if (await _scores.ExistsByCaptureIdAsync(capture.Id, ct))
            throw new DuplicateScoreException(command.CaptureId);

        var ocrResult = await _ocrResults.GetByCaptureIdAsync(capture.Id, ct)
            ?? throw new OcrResultNotFoundException(command.CaptureId);

        var answerKey = await _answerKeys.GetByExamIdAsync(capture.ExamId, ct)
            ?? new AnswerKey(new Dictionary<int, string>());

        var score = Score.Create(capture.Id, capture.ExamId, capture.StudentId, ocrResult.Answers, answerKey);

        await _scores.AddAsync(score, ct);
        await _auditLog.AppendAsync(
            AuditLog.Record(AuditAction.ScoreGenerated, captureId: capture.Id), ct);
        await _cache.InvalidateAsync(CacheKeys.Statistics, ct);

        return new ScoreCaptureResult(score.Id.Value, score.CorrectAnswers, score.TotalQuestions, score.Percentage);
    }
}
