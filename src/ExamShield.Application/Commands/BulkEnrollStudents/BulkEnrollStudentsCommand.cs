using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.BulkEnrollStudents;

public sealed record BulkEnrollStudentsResult(int Enrolled, int AlreadyEnrolled)
{
    public int Total => Enrolled + AlreadyEnrolled;
}

public sealed record BulkEnrollStudentsCommand(
    Guid ExamId,
    IReadOnlyList<Guid> StudentIds) : IRequest<BulkEnrollStudentsResult>;

public sealed class BulkEnrollStudentsCommandHandler(
    IExamRepository exams,
    IExamCandidateRepository candidates,
    IAuditLogRepository audit) : IRequestHandler<BulkEnrollStudentsCommand, BulkEnrollStudentsResult>
{
    public async Task<BulkEnrollStudentsResult> Handle(
        BulkEnrollStudentsCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        // Separate new students from duplicates up front so capacity can be checked atomically
        var newIds    = new List<Guid>();
        var skipCount = 0;
        foreach (var id in command.StudentIds)
        {
            if (await candidates.ExistsAsync(exam.Id, new StudentId(id), ct))
                skipCount++;
            else
                newIds.Add(id);
        }

        if (exam.MaxCandidates is not null)
        {
            var currentCount = await candidates.CountByExamIdAsync(exam.Id, ct);
            if (currentCount + newIds.Count > exam.MaxCandidates)
                throw new ExamFullException(command.ExamId, exam.MaxCandidates.Value);
        }

        foreach (var id in newIds)
            await candidates.AddAsync(ExamCandidate.Enroll(exam.Id, new StudentId(id)), ct);

        int enrolled = newIds.Count, skipped = skipCount;

        if (enrolled > 0)
            await audit.AppendAsync(
                AuditLog.Record(AuditAction.StudentEnrolled,
                    reason: $"{enrolled} students bulk-enrolled in exam {command.ExamId}"), ct);

        return new BulkEnrollStudentsResult(enrolled, skipped);
    }
}
