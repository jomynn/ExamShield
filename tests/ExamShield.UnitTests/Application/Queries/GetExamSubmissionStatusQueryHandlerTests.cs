using ExamShield.Application.Queries.GetExamSubmissionStatus;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetExamSubmissionStatusQueryHandlerTests
{
    private readonly IExamCandidateRepository _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly ICaptureRepository       _captures   = Substitute.For<ICaptureRepository>();
    private readonly GetExamSubmissionStatusQueryHandler _sut;

    private readonly ExamId _examId = ExamId.New();

    public GetExamSubmissionStatusQueryHandlerTests() =>
        _sut = new GetExamSubmissionStatusQueryHandler(_candidates, _captures);

    private static ExamCandidate MakeCandidate(ExamId examId, StudentId studentId)
    {
        var field = typeof(ExamCandidate).GetProperty("ExamId")!;
        var candidate = ExamCandidate.Enroll(examId, studentId);
        return candidate;
    }

    private static Capture MakeCapture(ExamId examId, StudentId studentId)
    {
        var hash = Hash.FromBytes(new byte[32]);
        return Capture.Create(examId, studentId, DeviceId.New(),
            new PageNumber(1), hash, new Signature(new byte[64]));
    }

    [Fact]
    public async Task Handle_AllStudentsSubmitted_ReturnsMissingZero()
    {
        var student = StudentId.New();
        _candidates.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { ExamCandidate.Enroll(_examId, student) });
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCapture(_examId, student) });

        var result = await _sut.Handle(new GetExamSubmissionStatusQuery(_examId.Value), default);

        Assert.Equal(1, result.TotalEnrolled);
        Assert.Equal(1, result.Submitted);
        Assert.Equal(0, result.Missing);
    }

    [Fact]
    public async Task Handle_NoSubmissions_ReturnsAllMissing()
    {
        var s1 = StudentId.New();
        var s2 = StudentId.New();
        _candidates.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { ExamCandidate.Enroll(_examId, s1), ExamCandidate.Enroll(_examId, s2) });
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new GetExamSubmissionStatusQuery(_examId.Value), default);

        Assert.Equal(2, result.TotalEnrolled);
        Assert.Equal(0, result.Submitted);
        Assert.Equal(2, result.Missing);
    }

    [Fact]
    public async Task Handle_PartialSubmission_ReturnsMissingCount()
    {
        var submitted = StudentId.New();
        var missing   = StudentId.New();
        _candidates.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] {
                ExamCandidate.Enroll(_examId, submitted),
                ExamCandidate.Enroll(_examId, missing)
            });
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCapture(_examId, submitted) });

        var result = await _sut.Handle(new GetExamSubmissionStatusQuery(_examId.Value), default);

        Assert.Equal(1, result.Submitted);
        Assert.Equal(1, result.Missing);
        Assert.Contains(result.Students, s => s.StudentId == missing.Value && !s.HasSubmitted);
        Assert.Contains(result.Students, s => s.StudentId == submitted.Value && s.HasSubmitted);
    }

    [Fact]
    public async Task Handle_NoEnrollments_ReturnsEmptyResult()
    {
        _candidates.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExamCandidate>());
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new GetExamSubmissionStatusQuery(_examId.Value), default);

        Assert.Equal(0, result.TotalEnrolled);
        Assert.Empty(result.Students);
    }

    [Fact]
    public async Task Handle_CaptureFromUnenrolledStudent_NotCountedAsSubmitted()
    {
        var enrolled    = StudentId.New();
        var unenrolled  = StudentId.New();
        _candidates.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { ExamCandidate.Enroll(_examId, enrolled) });
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCapture(_examId, unenrolled) });

        var result = await _sut.Handle(new GetExamSubmissionStatusQuery(_examId.Value), default);

        Assert.Equal(0, result.Submitted);
        Assert.Equal(1, result.Missing);
    }
}
