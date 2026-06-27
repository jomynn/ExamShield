using ExamShield.Application.Behaviors;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Behaviors;

public sealed class AlertBehaviorTests
{
    private readonly IAlertService _alertService = Substitute.For<IAlertService>();
    private readonly IRealtimeNotificationService _realtime = Substitute.For<IRealtimeNotificationService>();
    private readonly AlertBehavior<TestCommand, Unit> _sut;

    public AlertBehaviorTests() =>
        _sut = new AlertBehavior<TestCommand, Unit>(_alertService, _realtime);

    // MediatR 14: RequestHandlerDelegate<T> = Func<CancellationToken, Task<T>>
    private static RequestHandlerDelegate<Unit> Throwing(Exception ex) =>
        _ => Task.FromException<Unit>(ex);

    private static RequestHandlerDelegate<Unit> Succeeding() =>
        _ => Task.FromResult(Unit.Value);

    [Fact]
    public async Task Handle_WhenHashMismatchException_SendsHashMismatchAlert()
    {
        var ex = new HashMismatchException(Guid.NewGuid(),
            Hash.FromHex(new string('a', 64)), Hash.FromHex(new string('b', 64)));

        await Assert.ThrowsAsync<HashMismatchException>(() =>
            _sut.Handle(new TestCommand(), Throwing(ex), default));

        await _alertService.Received(1).SendAsync(
            AlertType.HashMismatch, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInvalidSignatureException_SendsInvalidSignatureAlert()
    {
        var ex = new InvalidSignatureException(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidSignatureException>(() =>
            _sut.Handle(new TestCommand(), Throwing(ex), default));

        await _alertService.Received(1).SendAsync(
            AlertType.InvalidSignature, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDuplicateUploadException_SendsDuplicateUploadAlert()
    {
        var ex = new DuplicateUploadException(Guid.NewGuid());

        await Assert.ThrowsAsync<DuplicateUploadException>(() =>
            _sut.Handle(new TestCommand(), Throwing(ex), default));

        await _alertService.Received(1).SendAsync(
            AlertType.DuplicateUpload, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOtherException_NoAlertSent()
    {
        var ex = new CaptureNotFoundException(Guid.NewGuid());

        await Assert.ThrowsAsync<CaptureNotFoundException>(() =>
            _sut.Handle(new TestCommand(), Throwing(ex), default));

        await _alertService.DidNotReceive().SendAsync(
            Arg.Any<AlertType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoException_NoAlertSent()
    {
        await _sut.Handle(new TestCommand(), Succeeding(), default);

        await _alertService.DidNotReceive().SendAsync(
            Arg.Any<AlertType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExceptionIsRethrown_AfterAlertIsSent()
    {
        var ex = new HashMismatchException(Guid.NewGuid(),
            Hash.FromHex(new string('a', 64)), Hash.FromHex(new string('b', 64)));

        var thrown = await Assert.ThrowsAsync<HashMismatchException>(() =>
            _sut.Handle(new TestCommand(), Throwing(ex), default));

        thrown.Should().BeSameAs(ex);
    }

    public sealed record TestCommand : IRequest<Unit>;
}
