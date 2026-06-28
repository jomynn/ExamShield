using ExamShield.Application.Queries.GetDevices;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetDevices;

public sealed class GetDevicesQueryHandlerTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly GetDevicesQueryHandler _sut;

    public GetDevicesQueryHandlerTests() =>
        _sut = new GetDevicesQueryHandler(_devices);

    private static Device MakeDevice(string name = "Scanner-01") =>
        Device.Register(name, new PublicKey(new byte[32]));

    [Fact]
    public async Task Handle_WithDevices_ReturnsMappedDtos()
    {
        var d1 = MakeDevice("A");
        var d2 = MakeDevice("B");
        _devices.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { d1, d2 });

        var result = await _sut.Handle(new GetDevicesQuery(), default);

        result.Devices.Should().HaveCount(2);
        result.Devices.Should().Contain(d => d.Name == "A");
        result.Devices.Should().Contain(d => d.Name == "B");
    }

    [Fact]
    public async Task Handle_MapsStatusAndIsActive()
    {
        var device = MakeDevice();
        _devices.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { device });

        var result = await _sut.Handle(new GetDevicesQuery(), default);

        var dto = result.Devices.Single();
        dto.Status.Should().Be(device.Status.ToString());
        dto.IsActive.Should().Be(device.IsActive);
        dto.DeviceId.Should().Be(device.Id.Value);
    }

    [Fact]
    public async Task Handle_NoDevices_ReturnsEmptyList()
    {
        _devices.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Device>());

        var result = await _sut.Handle(new GetDevicesQuery(), default);

        result.Devices.Should().BeEmpty();
    }
}
