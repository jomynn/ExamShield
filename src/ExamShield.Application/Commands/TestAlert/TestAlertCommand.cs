using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Commands.TestAlert;

public sealed record TestAlertResult(bool Sent, string? Error = null);
public sealed record TestAlertCommand : IRequest<TestAlertResult>;

public sealed class TestAlertCommandHandler(IAlertService alertService)
    : IRequestHandler<TestAlertCommand, TestAlertResult>
{
    public async Task<TestAlertResult> Handle(TestAlertCommand command, CancellationToken ct)
    {
        try
        {
            await alertService.SendAsync(
                AlertType.SuspiciousLogin,
                "ExamShield alert test — if you received this, your alert channel is configured correctly.",
                ct);
            return new TestAlertResult(Sent: true);
        }
        catch (Exception ex)
        {
            return new TestAlertResult(Sent: false, Error: ex.Message);
        }
    }
}
