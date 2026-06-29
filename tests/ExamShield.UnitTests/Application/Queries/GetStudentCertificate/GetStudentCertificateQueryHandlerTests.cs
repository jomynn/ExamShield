using ExamShield.Application.Queries.GetStudentCertificate;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries.GetStudentCertificate;

public sealed class GetStudentCertificateQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly IStudentCertificateService _certService = Substitute.For<IStudentCertificateService>();
    private readonly GetStudentCertificateQueryHandler _sut;

    public GetStudentCertificateQueryHandlerTests()
    {
        _sut = new GetStudentCertificateQueryHandler(_scores, _exams, _certService);
    }

    private static Score BuildPublishedScore(CaptureId captureId, ExamId examId)
    {
        var score = Score.Create(
            captureId, examId, StudentId.New(),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.95))],
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));
        score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_ReturnsRealPdf_WhenScorePublished()
    {
        var captureId = CaptureId.New();
        var examId = ExamId.New();
        var score = BuildPublishedScore(captureId, examId);
        var exam = Exam.Create("Final Exam 2026", null, 1);

        _scores.GetByCaptureIdAsync(captureId, default).Returns(score);
        _exams.GetByIdAsync(examId, default).Returns(exam);
        _certService.Generate(Arg.Any<CertificateData>()).Returns([0x25, 0x50, 0x44, 0x46]); // %PDF

        var result = await _sut.Handle(new GetStudentCertificateQuery(captureId.Value), default);

        result.PdfBytes.Should().NotBeEmpty();
        result.Filename.Should().StartWith("certificate-").And.EndWith(".pdf");
        _certService.Received(1).Generate(Arg.Is<CertificateData>(d =>
            d.ExamName == "Final Exam 2026" &&
            d.CorrectAnswers == 1 &&
            d.TotalQuestions == 1));
    }

    [Fact]
    public async Task Handle_ThrowsKeyNotFoundException_WhenScoreNotFound()
    {
        _scores.GetByCaptureIdAsync(Arg.Any<CaptureId>(), default).Returns((Score?)null);

        var act = () => _sut.Handle(new GetStudentCertificateQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsInvalidOperationException_WhenResultsNotPublished()
    {
        var captureId = CaptureId.New();
        var examId = ExamId.New();
        var unpublishedScore = Score.Create(
            captureId, examId, StudentId.New(),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.95))],
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));

        _scores.GetByCaptureIdAsync(captureId, default).Returns(unpublishedScore);

        var act = () => _sut.Handle(new GetStudentCertificateQuery(captureId.Value), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not been published*");
    }

    [Fact]
    public async Task Handle_ThrowsKeyNotFoundException_WhenExamNotFound()
    {
        var captureId = CaptureId.New();
        var examId = ExamId.New();
        var score = BuildPublishedScore(captureId, examId);

        _scores.GetByCaptureIdAsync(captureId, default).Returns(score);
        _exams.GetByIdAsync(examId, default).Returns((Exam?)null);

        var act = () => _sut.Handle(new GetStudentCertificateQuery(captureId.Value), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
