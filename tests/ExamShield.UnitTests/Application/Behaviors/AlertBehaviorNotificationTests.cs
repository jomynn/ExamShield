using ExamShield.Application.Behaviors;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Behaviors;

public sealed class AlertBehaviorNotificationTests
{
    private readonly IAlertService _alerts = Substitute.For<IAlertService>();
    private readonly IRealtimeNotificationService _realtime = Substitute.For<IRealtimeNotificationService>();
    private readonly AlertBehavior<TestRequest, Unit> _sut;

    public AlertBehaviorNotificationTests() =>
        _sut = new AlertBehavior<TestRequest, Unit>(_alerts, _realtime);

    public sealed record TestRequest : IRequest<Unit>;

    [Fact]
    public async Task Handle_OnHashMismatch_BroadcastsRealtimeNotification()
    {
        RequestHandlerDelegate<Unit> next = _ => throw new HashMismatchException(
            Guid.NewGuid(), Hash.FromHex(new string('a', 64)), Hash.FromHex(new string('b', 64)));

        await Assert.ThrowsAsync<HashMismatchException>(() =>
            _sut.Handle(new TestRequest(), next, default));

        await _realtime.Received(1).BroadcastAsync(
            Arg.Is<RealtimeNotification>(n =>
                n.Type == NotificationType.SecurityAlert &&
                n.Severity == NotificationSeverity.Critical),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnInvalidSignature_BroadcastsRealtimeNotification()
    {
        RequestHandlerDelegate<Unit> next = _ => throw new InvalidSignatureException(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidSignatureException>(() =>
            _sut.Handle(new TestRequest(), next, default));

        await _realtime.Received(1).BroadcastAsync(
            Arg.Is<RealtimeNotification>(n => n.Type == NotificationType.SecurityAlert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnDuplicateUpload_BroadcastsRealtimeNotification()
    {
        RequestHandlerDelegate<Unit> next = _ => throw new DuplicateUploadException(Guid.NewGuid());

        await Assert.ThrowsAsync<DuplicateUploadException>(() =>
            _sut.Handle(new TestRequest(), next, default));

        await _realtime.Received(1).BroadcastAsync(
            Arg.Is<RealtimeNotification>(n => n.Type == NotificationType.SecurityAlert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnSuccess_DoesNotBroadcast()
    {
        RequestHandlerDelegate<Unit> next = _ => Task.FromResult(Unit.Value);

        await _sut.Handle(new TestRequest(), next, default);

        await _realtime.DidNotReceive().BroadcastAsync(
            Arg.Any<RealtimeNotification>(), Arg.Any<CancellationToken>());
    }
}
