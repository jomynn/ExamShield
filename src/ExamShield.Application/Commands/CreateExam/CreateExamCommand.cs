using MediatR;

namespace ExamShield.Application.Commands.CreateExam;

public sealed record CreateExamResult(
    Guid ExamId, string Name, string? Description,
    int TotalQuestions, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt, DateTimeOffset? EndsAt,
    int? MaxCandidates = null);

public sealed record CreateExamCommand(
    string Name, string? Description, int TotalQuestions,
    DateTimeOffset? ScheduledAt = null, DateTimeOffset? EndsAt = null,
    int? MaxCandidates = null)
    : IRequest<CreateExamResult>;
