using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetDevices;

public sealed class GetDevicesQueryHandler(IDeviceRepository devices)
    : IRequestHandler<GetDevicesQuery, GetDevicesResult>
{
    public async Task<GetDevicesResult> Handle(GetDevicesQuery request, CancellationToken ct)
    {
        var all = await devices.ListAllAsync(ct);
        var dtos = all
            .Select(d => new DeviceDto(d.Id.Value, d.Name, d.IsActive, d.RegisteredAt, d.LastSeenAt))
            .ToList();
        return new GetDevicesResult(dtos);
    }
}
