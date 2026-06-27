using ExamShield.Application.Queries.GetSecurityEvents;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetSecurityEventsByCaptureTests
{
    private readonly ISecurityEventRepository _repo = Substitute.For<ISecurityEventRepository>();
    private readonly GetSecurityEventsQueryHandler _sut;

    public GetSecurityEventsByCaptureTests() => _sut = new GetSecurityEventsQueryHandler(_repo);

    private static SecurityEvent MakeEvent(Guid captureId) =>
        SecurityEvent.Create(SecurityEventType.DuplicateUpload, SecuritySeverity.Warning,
            "dup", captureId: captureId);

    [Fact]
    public async Task Handle_WithCaptureId_CallsListByCaptureId()
    {
        var captureId = Guid.NewGuid();
        var expected = new List<SecurityEvent> { MakeEvent(captureId) };
        _repo.ListByCaptureIdAsync(captureId, 100, default)
             .Returns(expected);

        await _sut.Handle(new GetSecurityEventsQuery(CaptureId: captureId), default);

        await _repo.Received(1).ListByCaptureIdAsync(captureId, 100, default);
        await _repo.DidNotReceive().ListRecentAsync(Arg.Any<int>(), default);
        await _repo.DidNotReceive().ListBySeverityAsync(Arg.Any<SecuritySeverity>(), Arg.Any<int>(), default);
    }

    [Fact]
    public async Task Handle_WithCaptureId_ReturnsMappedEvents()
    {
        var captureId = Guid.NewGuid();
        var evt = MakeEvent(captureId);
        _repo.ListByCaptureIdAsync(captureId, 100, default)
             .Returns(new List<SecurityEvent> { evt });

        var result = await _sut.Handle(new GetSecurityEventsQuery(CaptureId: captureId), default);

        var dto = Assert.Single(result.Events);
        Assert.Equal(captureId, dto.CaptureId);
    }

    [Fact]
    public async Task Handle_WithoutCaptureId_CallsListRecent()
    {
        _repo.ListRecentAsync(100, default).Returns(new List<SecurityEvent>());

        await _sut.Handle(new GetSecurityEventsQuery(), default);

        await _repo.Received(1).ListRecentAsync(100, default);
        await _repo.DidNotReceive().ListByCaptureIdAsync(Arg.Any<Guid>(), Arg.Any<int>(), default);
    }

    [Fact]
    public async Task Handle_CaptureIdTakesPrecedenceOverSeverity()
    {
        var captureId = Guid.NewGuid();
        _repo.ListByCaptureIdAsync(captureId, 50, default).Returns(new List<SecurityEvent>());

        await _sut.Handle(new GetSecurityEventsQuery(Limit: 50, Severity: "Critical", CaptureId: captureId), default);

        await _repo.Received(1).ListByCaptureIdAsync(captureId, 50, default);
        await _repo.DidNotReceive().ListBySeverityAsync(Arg.Any<SecuritySeverity>(), Arg.Any<int>(), default);
    }
}
