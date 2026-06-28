using ExamShield.Application.Queries.GetExamCandidates;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetExamCandidates;

public sealed class GetExamCandidatesQueryHandlerTests
{
    private readonly IExamCandidateRepository _repo = Substitute.For<IExamCandidateRepository>();
    private readonly GetExamCandidatesQueryHandler _sut;

    public GetExamCandidatesQueryHandlerTests() =>
        _sut = new GetExamCandidatesQueryHandler(_repo);

    [Fact]
    public async Task Handle_WithCandidates_ReturnsMappedDtos()
    {
        var examId = new ExamId(Guid.NewGuid());
        var c1 = ExamCandidate.Enroll(examId, new StudentId(Guid.NewGuid()));
        var c2 = ExamCandidate.Enroll(examId, new StudentId(Guid.NewGuid()));
        _repo.ListByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(new[] { c1, c2 });

        var result = await _sut.Handle(new GetExamCandidatesQuery(examId.Value), default);

        result.ExamId.Should().Be(examId.Value);
        result.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoCandidates_ReturnsEmptyList()
    {
        var examId = new ExamId(Guid.NewGuid());
        IReadOnlyList<ExamCandidate> empty = [];
        _repo.ListByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(empty);

        var result = await _sut.Handle(new GetExamCandidatesQuery(examId.Value), default);

        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsStudentIdAndEnrolledAt()
    {
        var examId = new ExamId(Guid.NewGuid());
        var studentId = new StudentId(Guid.NewGuid());
        var candidate = ExamCandidate.Enroll(examId, studentId);
        _repo.ListByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(new[] { candidate });

        var result = await _sut.Handle(new GetExamCandidatesQuery(examId.Value), default);

        result.Candidates[0].StudentId.Should().Be(studentId.Value);
        result.Candidates[0].EnrolledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
