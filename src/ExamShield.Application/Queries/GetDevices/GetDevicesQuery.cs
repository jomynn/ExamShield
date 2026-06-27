using MediatR;

namespace ExamShield.Application.Queries.GetDevices;

public sealed record GetDevicesQuery : IRequest<GetDevicesResult>;

public sealed record DeviceDto(Guid DeviceId, string Name, bool IsActive, DateTimeOffset RegisteredAt, DateTimeOffset? LastSeenAt);

public sealed record GetDevicesResult(IReadOnlyList<DeviceDto> Devices);
