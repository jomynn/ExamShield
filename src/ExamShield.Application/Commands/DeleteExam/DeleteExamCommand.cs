using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.DeleteExam;

public sealed record DeleteExamCommand(Guid ExamId) : IRequest;

public sealed class DeleteExamCommandHandler(IExamRepository exams)
    : IRequestHandler<DeleteExamCommand>
{
    public async Task Handle(DeleteExamCommand command, CancellationToken ct)
    {
        var exam = await exams.GetByIdAsync(new ExamId(command.ExamId), ct)
            ?? throw new KeyNotFoundException($"Exam {command.ExamId} not found.");

        exam.MarkDeleted();
        await exams.UpdateAsync(exam, ct);
    }
}
