using System.Diagnostics;
using ExamShield.Application.Behaviors;
using FluentAssertions;
using MediatR;
using Xunit;

namespace ExamShield.UnitTests.Application.Behaviors;

public sealed class TracingBehaviorTests
{
    private sealed record TestRequest : IRequest<string>;

    private static TracingBehavior<TestRequest, string> MakeSut(ActivitySource source) =>
        new(source);

    [Fact]
    public async Task Handle_SuccessfulNext_ReturnsNextResult()
    {
        using var source = new ActivitySource("Test.Source.Success", "1.0.0");
        var sut    = MakeSut(source);
        var result = await sut.Handle(new TestRequest(), _ => Task.FromResult("ok"), default);
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_NoListenerAttached_StillReturnsNextResult()
    {
        // Without a listener, StartActivity returns null; behavior falls through gracefully.
        using var source = new ActivitySource("Test.Source.NoListener", "1.0.0");
        var sut    = MakeSut(source);
        var result = await sut.Handle(new TestRequest(), _ => Task.FromResult("passthrough"), default);
        result.Should().Be("passthrough");
    }

    [Fact]
    public async Task Handle_NextThrows_PropagatesException()
    {
        using var source = new ActivitySource("Test.Source.Throw", "1.0.0");
        using var listener = new ActivityListener
        {
            ShouldListenTo    = _ => true,
            Sample            = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted   = _ => { },
            ActivityStopped   = _ => { },
        };
        ActivitySource.AddActivityListener(listener);

        var sut = MakeSut(source);

        await sut.Invoking(s => s.Handle(
                new TestRequest(),
                _ => throw new InvalidOperationException("boom"),
                default))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    [Fact]
    public async Task Handle_WithListener_CreatesActivityNamedAfterRequest()
    {
        string? capturedName = null;
        using var source = new ActivitySource("Test.Source.Named", "1.0.0");
        using var listener = new ActivityListener
        {
            ShouldListenTo    = _ => true,
            Sample            = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted   = a => capturedName = a.OperationName,
            ActivityStopped   = _ => { },
        };
        ActivitySource.AddActivityListener(listener);

        var sut = MakeSut(source);
        await sut.Handle(new TestRequest(), _ => Task.FromResult("x"), default);

        capturedName.Should().Be(nameof(TestRequest));
    }

    [Fact]
    public async Task Handle_WithListener_ActivityHasMediatRRequestTag()
    {
        string? capturedTag = null;
        using var source = new ActivitySource("Test.Source.Tag", "1.0.0");
        using var listener = new ActivityListener
        {
            ShouldListenTo    = _ => true,
            Sample            = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted   = _ => { },
            ActivityStopped   = a => capturedTag = a.GetTagItem("mediatR.request") as string,
        };
        ActivitySource.AddActivityListener(listener);

        var sut = MakeSut(source);
        await sut.Handle(new TestRequest(), _ => Task.FromResult("x"), default);

        capturedTag.Should().Contain(nameof(TestRequest));
    }
}
