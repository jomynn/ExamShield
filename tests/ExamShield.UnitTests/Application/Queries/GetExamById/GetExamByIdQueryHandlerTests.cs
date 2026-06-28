using ExamShield.Application.Queries.GetExamById;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetExamById;

public sealed class GetExamByIdQueryHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly GetExamByIdQueryHandler _sut;

    public GetExamByIdQueryHandlerTests() =>
        _sut = new GetExamByIdQueryHandler(_exams);

    private static Exam MakeExam() =>
        Exam.Create("Biology Final", "Desc", 50, null, null, 100);

    [Fact]
    public async Task Handle_ExistingExam_ReturnsMappedDto()
    {
        var exam = MakeExam();
        _exams.GetByIdAsync(exam.Id, Arg.Any<CancellationToken>()).Returns(exam);

        var result = await _sut.Handle(new GetExamByIdQuery(exam.Id.Value), default);

        result.Should().NotBeNull();
        result!.ExamId.Should().Be(exam.Id.Value);
        result.Name.Should().Be("Biology Final");
        result.TotalQuestions.Should().Be(50);
        result.Status.Should().Be(exam.Status.ToString());
    }

    [Fact]
    public async Task Handle_MissingExam_ReturnsNull()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
              .Returns((Exam?)null);

        var result = await _sut.Handle(new GetExamByIdQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }
}
