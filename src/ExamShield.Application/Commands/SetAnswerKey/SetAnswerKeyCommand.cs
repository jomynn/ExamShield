using MediatR;

namespace ExamShield.Application.Commands.SetAnswerKey;

public sealed record SetAnswerKeyCommand(Guid ExamId, IReadOnlyDictionary<int, string> Answers)
    : IRequest;
