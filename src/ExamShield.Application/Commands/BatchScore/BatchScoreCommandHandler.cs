using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.BatchScore;

public sealed class BatchScoreCommandHandler(
    IExamRepository exams,
    ICaptureRepository captures,
    IOcrResultRepository ocrResults,
    IScoreRepository scores,
    ISender sender) : IRequestHandler<BatchScoreCommand, BatchScoreResult>
{
    public async Task<BatchScoreResult> Handle(BatchScoreCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        var all = await captures.ListByExamIdAsync(exam.Id, ct);
        if (all.Count == 0)
            return new BatchScoreResult(0, 0);

        var existingScores = await scores.GetByExamIdAsync(exam.Id, ct);
        var scoredIds = existingScores.Select(s => s.CaptureId).ToHashSet();

        var unscoredCaptures = all.Where(c => !scoredIds.Contains(c.Id)).ToList();
        var unscoredIds = unscoredCaptures.Select(c => c.Id).ToList();

        var completedOcr = await ocrResults.ListByCaptureIdsAsync(unscoredIds, ct);
        var eligibleIds  = completedOcr.Select(r => r.CaptureId).ToHashSet();

        var eligible = unscoredCaptures.Where(c => eligibleIds.Contains(c.Id)).ToList();

        foreach (var capture in eligible)
            await sender.Send(new ScoreCaptureCommand(capture.Id.Value), ct);

        return new BatchScoreResult(eligible.Count, all.Count - eligible.Count);
    }
}
