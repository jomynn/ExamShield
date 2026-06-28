using ExamShield.Application.Queries.GetReportSummary;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetReportSummary;

public sealed class GetReportSummaryQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ISecurityEventRepository _securityEvents = Substitute.For<ISecurityEventRepository>();
    private readonly GetReportSummaryQueryHandler _sut;

    public GetReportSummaryQueryHandlerTests() =>
        _sut = new GetReportSummaryQueryHandler(_captures, _ocrResults, _scores, _securityEvents);

    private void SetupEmpty()
    {
        _captures.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Capture>());
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<OcrResult>());
        _scores.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Score>());
        _securityEvents.CountAllAsync(Arg.Any<CancellationToken>()).Returns(0);
        _securityEvents.CountBySeverityAsync(SecuritySeverity.Critical, Arg.Any<CancellationToken>()).Returns(0);
    }

    private static Capture MakeCapture(bool uploaded = false, bool verified = false)
    {
        var hash = Hash.FromBytes(new byte[32]);
        var c = Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            hash, new Signature(new byte[64]));
        if (uploaded || verified) c.RecordUpload("key");
        if (verified) c.VerifyIntegrity(hash);
        return c;
    }

    [Fact]
    public async Task Handle_EmptySystem_ReturnsZeroStats()
    {
        SetupEmpty();

        var result = await _sut.Handle(new GetReportSummaryQuery(), default);

        result.Captures.Total.Should().Be(0);
        result.Ocr.TotalProcessed.Should().Be(0);
        result.Scores.TotalScored.Should().Be(0);
        result.Security.TotalEvents.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCaptures_ReportsCaptureStatusBreakdown()
    {
        var created = MakeCapture();
        var uploaded = MakeCapture(uploaded: true);
        var verified = MakeCapture(verified: true);
        _captures.ListAllAsync(Arg.Any<CancellationToken>())
                 .Returns(new[] { created, uploaded, verified });
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<OcrResult>());
        _scores.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Score>());
        _securityEvents.CountAllAsync(Arg.Any<CancellationToken>()).Returns(0);
        _securityEvents.CountBySeverityAsync(Arg.Any<SecuritySeverity>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await _sut.Handle(new GetReportSummaryQuery(), default);

        result.Captures.Total.Should().Be(3);
        result.Captures.Created.Should().Be(1);
        result.Captures.Uploaded.Should().Be(1);
        result.Captures.Verified.Should().Be(1);
        result.Captures.Tampered.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithOcrResults_ReportsAverageConfidence()
    {
        SetupEmpty();
        var ocr = OcrResult.Create(new CaptureId(Guid.NewGuid()),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.9))]);
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(new[] { ocr });

        var result = await _sut.Handle(new GetReportSummaryQuery(), default);

        result.Ocr.TotalProcessed.Should().Be(1);
        result.Ocr.AverageConfidence.Should().BeApproximately(0.9, 0.01);
    }

    [Fact]
    public async Task Handle_WithSecurityEvents_ReportsTotals()
    {
        SetupEmpty();
        _securityEvents.CountAllAsync(Arg.Any<CancellationToken>()).Returns(15);
        _securityEvents.CountBySeverityAsync(SecuritySeverity.Critical, Arg.Any<CancellationToken>()).Returns(3);

        var result = await _sut.Handle(new GetReportSummaryQuery(), default);

        result.Security.TotalEvents.Should().Be(15);
        result.Security.CriticalEvents.Should().Be(3);
    }
}
