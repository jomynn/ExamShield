using ExamShield.Application.Commands.UpdateSettings;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.UpdateSettings;

public sealed class UpdateSettingsAuditTests
{
    private readonly ISystemSettingsRepository _settings = Substitute.For<ISystemSettingsRepository>();
    private readonly IAuditLogRepository       _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly UpdateSettingsCommandHandler _sut;

    public UpdateSettingsAuditTests() =>
        _sut = new UpdateSettingsCommandHandler(_settings, _auditLog);

    [Fact]
    public async Task Handle_SettingsUpdate_AppendsSettingsUpdatedAuditEntry()
    {
        var settings = SystemSettings.CreateDefault();
        _settings.GetAsync(default).Returns(settings);

        await _sut.Handle(
            new UpdateSettingsCommand(0.8, true, "Warning", 60, 7), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.SettingsUpdated), default);
    }

    [Fact]
    public async Task Handle_SettingsUpdate_AuditAfterSave()
    {
        var settings = SystemSettings.CreateDefault();
        _settings.GetAsync(default).Returns(settings);

        await _sut.Handle(
            new UpdateSettingsCommand(0.8, true, "Warning", 60, 7), default);

        Received.InOrder(() =>
        {
            _settings.SaveAsync(settings, default);
            _auditLog.AppendAsync(Arg.Any<AuditLog>(), default);
        });
    }
}
