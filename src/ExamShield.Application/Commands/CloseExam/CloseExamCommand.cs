using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.CloseExam;

public sealed record CloseExamCommand(Guid ExamId) : IRequest;

public sealed class CloseExamCommandHandler(IExamRepository exams)
    : IRequestHandler<CloseExamCommand>
{
    public async Task Handle(CloseExamCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam '{command.ExamId}' not found.");
        exam.Close();
        await exams.UpdateAsync(exam, ct);
    }
}
