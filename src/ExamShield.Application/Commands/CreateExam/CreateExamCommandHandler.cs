using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Commands.CreateExam;

public sealed class CreateExamCommandHandler(IExamRepository exams)
    : IRequestHandler<CreateExamCommand, CreateExamResult>
{
    public async Task<CreateExamResult> Handle(CreateExamCommand request, CancellationToken ct)
    {
        var exam = Exam.Create(request.Name, request.Description, request.TotalQuestions, request.ScheduledAt, request.EndsAt);
        await exams.AddAsync(exam, ct);
        return new CreateExamResult(
            exam.Id.Value, exam.Name, exam.Description,
            exam.TotalQuestions, exam.Status.ToString(), exam.CreatedAt,
            exam.ScheduledAt, exam.EndsAt);
    }
}
