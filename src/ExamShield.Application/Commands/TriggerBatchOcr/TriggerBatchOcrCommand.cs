using MediatR;

namespace ExamShield.Application.Commands.TriggerBatchOcr;

public sealed record TriggerBatchOcrResult(int Queued, int Skipped);
public sealed record TriggerBatchOcrCommand(Guid ExamId) : IRequest<TriggerBatchOcrResult>;
