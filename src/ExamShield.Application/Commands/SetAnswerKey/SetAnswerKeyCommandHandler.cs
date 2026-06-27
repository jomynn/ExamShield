using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.SetAnswerKey;

public sealed class SetAnswerKeyCommandHandler(
    IExamRepository exams,
    IAnswerKeyRepository answerKeys,
    IAuditLogRepository auditLog) : IRequestHandler<SetAnswerKeyCommand>
{
    public async Task Handle(SetAnswerKeyCommand command, CancellationToken ct)
    {
        if (command.Answers.Count == 0)
            throw new ArgumentException("Answer key must contain at least one answer.");

        var examId = new ExamId(command.ExamId);
        var exam = await exams.GetByIdAsync(examId, ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        var existing = await answerKeys.GetEntityByExamIdAsync(exam.Id, ct);
        if (existing is not null)
            throw new AnswerKeyAlreadySetException(command.ExamId);

        var key = ExamAnswerKey.Create(exam.Id, command.Answers);
        await answerKeys.SaveAsync(key, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.AnswerKeySet), ct);
    }
}
