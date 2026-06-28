using ExamShield.Application.Queries.GetNotificationSettings;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetNotificationSettings;

public sealed class GetNotificationSettingsQueryHandlerTests
{
    private readonly INotificationChannelSettingsRepository _repo =
        Substitute.For<INotificationChannelSettingsRepository>();
    private readonly GetNotificationSettingsQueryHandler _sut;

    public GetNotificationSettingsQueryHandlerTests() =>
        _sut = new GetNotificationSettingsQueryHandler(_repo);

    [Fact]
    public async Task Handle_ReturnsDefaultSettings()
    {
        var settings = NotificationChannelSettings.CreateDefault();
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.Handle(new GetNotificationSettingsQuery(), default);

        result.Should().NotBeNull();
        result.EmailEnabled.Should().BeFalse();
        result.SlackEnabled.Should().BeFalse();
        result.LineEnabled.Should().BeFalse();
        result.WebhookEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithEmailEnabled_ReturnsEmailSettings()
    {
        var settings = NotificationChannelSettings.CreateDefault();
        settings.Update(true, "admin@exam.io", false, null, false, null, false, null);
        _repo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var result = await _sut.Handle(new GetNotificationSettingsQuery(), default);

        result.EmailEnabled.Should().BeTrue();
        result.EmailRecipients.Should().Be("admin@exam.io");
    }
}
