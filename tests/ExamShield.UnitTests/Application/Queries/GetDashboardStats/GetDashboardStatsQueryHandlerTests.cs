using ExamShield.Application.Queries.GetDashboardStats;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetDashboardStats;

public sealed class GetDashboardStatsQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IManualReviewRepository _reviews = Substitute.For<IManualReviewRepository>();
    private readonly ISecurityEventRepository _securityEvents = Substitute.For<ISecurityEventRepository>();
    private readonly GetDashboardStatsQueryHandler _sut;

    public GetDashboardStatsQueryHandlerTests() =>
        _sut = new GetDashboardStatsQueryHandler(_captures, _reviews, _securityEvents);

    private void SetupDefaults(int total = 0, int verified = 0,
        IReadOnlyList<ManualReview>? pending = null,
        IReadOnlyList<SecurityEvent>? alerts = null)
    {
        _captures.CountAsync(Arg.Any<CancellationToken>()).Returns(total);
        _captures.CountVerifiedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                 .Returns(verified);
        _reviews.GetPendingAsync(Arg.Any<CancellationToken>())
                .Returns(pending ?? Array.Empty<ManualReview>());
        _securityEvents.ListRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                       .Returns(alerts ?? Array.Empty<SecurityEvent>());
    }

    [Fact]
    public async Task Handle_EmptySystem_ReturnsZeroCounts()
    {
        SetupDefaults();

        var result = await _sut.Handle(new GetDashboardStatsQuery(), default);

        result.TotalCaptures.Should().Be(0);
        result.PendingReview.Should().Be(0);
        result.VerifiedToday.Should().Be(0);
        result.ActiveAlerts.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCaptures_ReturnsTotalCaptures()
    {
        SetupDefaults(total: 42, verified: 10);

        var result = await _sut.Handle(new GetDashboardStatsQuery(), default);

        result.TotalCaptures.Should().Be(42);
        result.VerifiedToday.Should().Be(10);
    }

    [Fact]
    public async Task Handle_WithPendingReviews_ReturnsPendingCount()
    {
        var ocr = OcrResult.Create(new CaptureId(Guid.NewGuid()),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.3))]);
        var review = ManualReview.CreateFor(ocr);
        SetupDefaults(pending: new[] { review });

        var result = await _sut.Handle(new GetDashboardStatsQuery(), default);

        result.PendingReview.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CriticalAlertsWithin24h_CountsAsActiveAlerts()
    {
        var critical = SecurityEvent.Create(
            SecurityEventType.HashMismatch, SecuritySeverity.Critical, "Hash mismatch");
        SetupDefaults(alerts: new[] { critical });

        var result = await _sut.Handle(new GetDashboardStatsQuery(), default);

        result.ActiveAlerts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonCriticalAlerts_DoNotCountAsActive()
    {
        var info = SecurityEvent.Create(
            SecurityEventType.LoginSuccess, SecuritySeverity.Info, "User logged in");
        SetupDefaults(alerts: new[] { info });

        var result = await _sut.Handle(new GetDashboardStatsQuery(), default);

        result.ActiveAlerts.Should().Be(0);
    }
}
