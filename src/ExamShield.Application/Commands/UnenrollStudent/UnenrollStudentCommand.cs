using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.UnenrollStudent;

public sealed record UnenrollStudentCommand(Guid ExamId, Guid StudentId) : IRequest;

public sealed class UnenrollStudentCommandHandler(
    IExamCandidateRepository candidates,
    ICaptureRepository captures,
    IExamRepository exams,
    IAuditLogRepository audit)
    : IRequestHandler<UnenrollStudentCommand>
{
    public async Task Handle(UnenrollStudentCommand command, CancellationToken ct)
    {
        var examId    = new ExamId(command.ExamId);
        var studentId = new StudentId(command.StudentId);

        _ = await exams.GetByIdAsync(examId, ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        if (!await candidates.ExistsAsync(examId, studentId, ct))
            throw new StudentNotEnrolledException(command.ExamId, command.StudentId);

        var studentCaptures = await captures.ListByStudentIdAsync(studentId, ct);
        var hasCapture = studentCaptures.Any(c => c.ExamId == examId);
        if (hasCapture)
            throw new StudentHasCaptureException(command.ExamId, command.StudentId);

        await candidates.RemoveAsync(examId, studentId, ct);
        await audit.AppendAsync(
            AuditLog.Record(AuditAction.StudentUnenrolled,
                reason: $"Exam:{command.ExamId} Student:{command.StudentId}"), ct);
    }
}
