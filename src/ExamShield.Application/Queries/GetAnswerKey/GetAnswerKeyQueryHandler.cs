using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetAnswerKey;

public sealed class GetAnswerKeyQueryHandler(IAnswerKeyRepository answerKeys)
    : IRequestHandler<GetAnswerKeyQuery, GetAnswerKeyResult>
{
    public async Task<GetAnswerKeyResult> Handle(GetAnswerKeyQuery query, CancellationToken ct)
    {
        var key = await answerKeys.GetEntityByExamIdAsync(new ExamId(query.ExamId), ct)
            ?? throw new KeyNotFoundException($"No answer key found for exam {query.ExamId}.");

        return new GetAnswerKeyResult(query.ExamId, key.Answers, key.CreatedAt);
    }
}
