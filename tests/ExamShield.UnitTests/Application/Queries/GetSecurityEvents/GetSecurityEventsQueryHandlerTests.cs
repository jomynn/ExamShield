using ExamShield.Application.Queries.GetSecurityEvents;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetSecurityEvents;

public sealed class GetSecurityEventsQueryHandlerTests
{
    private readonly ISecurityEventRepository _repo = Substitute.For<ISecurityEventRepository>();
    private readonly GetSecurityEventsQueryHandler _sut;

    public GetSecurityEventsQueryHandlerTests() => _sut = new(_repo);

    private static SecurityEvent MakeEvent(SecurityEventType type = SecurityEventType.LoginFailed) =>
        SecurityEvent.Create(type, SecuritySeverity.Warning, "test event");

    [Fact]
    public async Task Handle_NoCaptureIdNorSeverity_CallsListRecent()
    {
        IReadOnlyList<SecurityEvent> events = [];
        _repo.ListRecentAsync(Arg.Any<int>(), default).Returns(events);

        await _sut.Handle(new(Limit: 10), default);

        await _repo.Received(1).ListRecentAsync(10, default);
        await _repo.DidNotReceive().ListByCaptureIdAsync(Arg.Any<Guid>(), Arg.Any<int>(), default);
        await _repo.DidNotReceive().ListBySeverityAsync(Arg.Any<SecuritySeverity>(), Arg.Any<int>(), default);
    }

    [Fact]
    public async Task Handle_WithCaptureId_CallsListByCaptureId()
    {
        var captureId = Guid.NewGuid();
        IReadOnlyList<SecurityEvent> events = [];
        _repo.ListByCaptureIdAsync(captureId, Arg.Any<int>(), default).Returns(events);

        await _sut.Handle(new(CaptureId: captureId, Limit: 20), default);

        await _repo.Received(1).ListByCaptureIdAsync(captureId, 20, default);
    }

    [Fact]
    public async Task Handle_WithSeverity_CallsListBySeverity()
    {
        IReadOnlyList<SecurityEvent> events = [];
        _repo.ListBySeverityAsync(SecuritySeverity.Critical, Arg.Any<int>(), default).Returns(events);

        await _sut.Handle(new(Severity: "Critical", Limit: 5), default);

        await _repo.Received(1).ListBySeverityAsync(SecuritySeverity.Critical, 5, default);
    }

    [Fact]
    public async Task Handle_InvalidSeverityString_FallsBackToListRecent()
    {
        IReadOnlyList<SecurityEvent> events = [];
        _repo.ListRecentAsync(Arg.Any<int>(), default).Returns(events);

        await _sut.Handle(new(Severity: "NotASeverity", Limit: 10), default);

        await _repo.Received(1).ListRecentAsync(10, default);
    }

    [Fact]
    public async Task Handle_MapsAllDtoFields()
    {
        var evt = SecurityEvent.Create(
            SecurityEventType.HashMismatch, SecuritySeverity.Critical, "Hash mismatch detected",
            userId: "u1", ipAddress: "10.0.0.1");
        IReadOnlyList<SecurityEvent> events = [evt];
        _repo.ListRecentAsync(Arg.Any<int>(), default).Returns(events);

        var result = await _sut.Handle(new(Limit: 10), default);

        var dto = result.Events[0];
        dto.EventType.Should().Be("HashMismatch");
        dto.Severity.Should().Be("Critical");
        dto.Message.Should().Be("Hash mismatch detected");
        dto.UserId.Should().Be("u1");
        dto.IpAddress.Should().Be("10.0.0.1");
    }
}
