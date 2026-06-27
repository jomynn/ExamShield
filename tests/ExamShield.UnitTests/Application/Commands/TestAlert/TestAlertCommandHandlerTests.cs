using ExamShield.Application.Commands.TestAlert;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.TestAlert;

public sealed class TestAlertCommandHandlerTests
{
    private readonly IAlertService _alert = Substitute.For<IAlertService>();
    private readonly TestAlertCommandHandler _sut;

    public TestAlertCommandHandlerTests() => _sut = new TestAlertCommandHandler(_alert);

    [Fact]
    public async Task Handle_CallsAlertServiceWithTestMessage()
    {
        await _sut.Handle(new TestAlertCommand(), default);

        await _alert.Received(1).SendAsync(
            Arg.Any<AlertType>(),
            Arg.Is<string>(m => m.Contains("test") || m.Contains("Test")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccessResult()
    {
        var result = await _sut.Handle(new TestAlertCommand(), default);

        Assert.True(result.Sent);
    }

    [Fact]
    public async Task Handle_AlertServiceThrows_ReturnsFailed()
    {
        _alert.SendAsync(Arg.Any<AlertType>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("channel unreachable")));

        var result = await _sut.Handle(new TestAlertCommand(), default);

        Assert.False(result.Sent);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Handle_UsesCorrectAlertType()
    {
        await _sut.Handle(new TestAlertCommand(), default);

        await _alert.Received(1).SendAsync(
            AlertType.SuspiciousLogin,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
