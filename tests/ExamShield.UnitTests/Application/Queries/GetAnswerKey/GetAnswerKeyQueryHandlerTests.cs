using ExamShield.Application.Queries.GetAnswerKey;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetAnswerKey;

public sealed class GetAnswerKeyQueryHandlerTests
{
    private readonly IAnswerKeyRepository _answerKeys = Substitute.For<IAnswerKeyRepository>();
    private readonly GetAnswerKeyQueryHandler _sut;

    public GetAnswerKeyQueryHandlerTests() =>
        _sut = new GetAnswerKeyQueryHandler(_answerKeys);

    [Fact]
    public async Task Handle_ExistingKey_ReturnsMappedResult()
    {
        var examId = new ExamId(Guid.NewGuid());
        var answers = new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };
        var key = ExamAnswerKey.Create(examId, answers);
        _answerKeys.GetEntityByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(key);

        var result = await _sut.Handle(new GetAnswerKeyQuery(examId.Value), default);

        result.ExamId.Should().Be(examId.Value);
        result.Answers.Should().HaveCount(3);
        result.Answers[1].Should().Be("A");
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_MissingKey_ThrowsKeyNotFoundException()
    {
        _answerKeys.GetEntityByExamIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
                   .Returns((ExamAnswerKey?)null);

        var act = () => _sut.Handle(new GetAnswerKeyQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
