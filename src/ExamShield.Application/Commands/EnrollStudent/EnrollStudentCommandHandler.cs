using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.EnrollStudent;

public sealed class EnrollStudentCommandHandler(
    IExamRepository exams,
    IExamCandidateRepository candidates,
    IAuditLogRepository audit) : IRequestHandler<EnrollStudentCommand>
{
    public async Task Handle(EnrollStudentCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        var studentId = new StudentId(command.StudentId);

        if (await candidates.ExistsAsync(exam.Id, studentId, ct))
            throw new StudentAlreadyEnrolledException(command.ExamId, command.StudentId);

        if (exam.MaxCandidates is not null)
        {
            var enrolled = await candidates.CountByExamIdAsync(exam.Id, ct);
            if (enrolled >= exam.MaxCandidates)
                throw new ExamFullException(command.ExamId, exam.MaxCandidates.Value);
        }

        var candidate = ExamCandidate.Enroll(exam.Id, studentId);
        await candidates.AddAsync(candidate, ct);
        await audit.AppendAsync(
            AuditLog.Record(AuditAction.StudentEnrolled, reason: $"Student {command.StudentId} enrolled in exam {command.ExamId}"),
            ct);
    }
}
