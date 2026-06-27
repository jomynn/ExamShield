using MediatR;

namespace ExamShield.Application.Commands.EnrollStudent;

public sealed record EnrollStudentCommand(Guid ExamId, Guid StudentId) : IRequest;
