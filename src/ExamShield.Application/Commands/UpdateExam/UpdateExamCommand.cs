using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.UpdateExam;

public sealed record UpdateExamCommand(
    Guid ExamId,
    string Name,
    string? Description,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? EndsAt) : IRequest;

public sealed class UpdateExamCommandHandler(IExamRepository exams)
    : IRequestHandler<UpdateExamCommand>
{
    public async Task Handle(UpdateExamCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        exam.Update(command.Name, command.Description, command.ScheduledAt, command.EndsAt);
        await exams.UpdateAsync(exam, ct);
    }
}
