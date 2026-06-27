using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.ActivateExam;

public sealed record ActivateExamCommand(Guid ExamId) : IRequest;

public sealed class ActivateExamCommandHandler(IExamRepository exams)
    : IRequestHandler<ActivateExamCommand>
{
    public async Task Handle(ActivateExamCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam '{command.ExamId}' not found.");
        exam.Activate();
        await exams.UpdateAsync(exam, ct);
    }
}
