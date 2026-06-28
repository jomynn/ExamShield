using ExamShield.Application.Queries.GetSettings;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetSettings;

public sealed class GetSettingsQueryHandlerTests
{
    private readonly ISystemSettingsRepository _repo = Substitute.For<ISystemSettingsRepository>();
    private readonly GetSettingsQueryHandler _sut;

    public GetSettingsQueryHandlerTests() =>
        _sut = new GetSettingsQueryHandler(_repo);

    [Fact]
    public async Task Handle_ReturnsDefaultSettings()
    {
        var settings = SystemSettings.CreateDefault();
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.Handle(new GetSettingsQuery(), default);

        result.Should().NotBeNull();
        result.OcrConfidenceThreshold.Should().BeGreaterThan(0);
        result.AccessTokenExpiryMinutes.Should().BeGreaterThan(0);
        result.RefreshTokenExpiryDays.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_MapsAllFields()
    {
        var settings = SystemSettings.CreateDefault();
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.Handle(new GetSettingsQuery(), default);

        result.OcrConfidenceThreshold.Should().Be(settings.OcrConfidenceThreshold);
        result.NotificationsEnabled.Should().Be(settings.NotificationsEnabled);
        result.NotificationSeverity.Should().Be(settings.NotificationSeverity);
        result.AccessTokenExpiryMinutes.Should().Be(settings.AccessTokenExpiryMinutes);
        result.RefreshTokenExpiryDays.Should().Be(settings.RefreshTokenExpiryDays);
    }
}
