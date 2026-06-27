using ExamShield.Application.Commands.TriggerOcr;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.TriggerBatchOcr;

public sealed class TriggerBatchOcrCommandHandler(
    IExamRepository exams,
    ICaptureRepository captures,
    ISender sender) : IRequestHandler<TriggerBatchOcrCommand, TriggerBatchOcrResult>
{
    public async Task<TriggerBatchOcrResult> Handle(TriggerBatchOcrCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        var all = await captures.ListByExamIdAsync(exam.Id, ct);
        var eligible = all
            .Where(c => c.Status == CaptureStatus.Uploaded || c.Status == CaptureStatus.Verified)
            .ToList();

        foreach (var capture in eligible)
            await sender.Send(new TriggerOcrCommand(capture.Id.Value), ct);

        return new TriggerBatchOcrResult(eligible.Count, all.Count - eligible.Count);
    }
}
