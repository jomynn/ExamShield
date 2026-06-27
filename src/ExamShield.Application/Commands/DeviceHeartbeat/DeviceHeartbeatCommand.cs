using MediatR;

namespace ExamShield.Application.Commands.DeviceHeartbeat;

public sealed record DeviceHeartbeatResult(Guid DeviceId, DateTimeOffset LastSeenAt);
public sealed record DeviceHeartbeatCommand(Guid DeviceId) : IRequest<DeviceHeartbeatResult>;
