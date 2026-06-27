using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Persistence;
using ExamShield.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.IntegrationTests.Infrastructure.Repositories;

public sealed class DeviceRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ExamShieldDbContext _context = null!;
    private DeviceRepository _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ExamShieldDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ExamShieldDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _sut = new DeviceRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Device MakeDevice(string name = "Scanner-01") =>
        Device.Register(name, new PublicKey(new byte[] { 0x04, 0x01, 0x02, 0x03 }));

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsDevice()
    {
        var device = MakeDevice();

        await _sut.AddAsync(device);
        var loaded = await _sut.GetByIdAsync(device.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(device.Id);
    }

    [Fact]
    public async Task AddAsync_PreservesAllFields()
    {
        var keyBytes = new byte[] { 0x04, 0xAB, 0xCD, 0xEF };
        var device = Device.Register("Mobile-Scanner-07", new PublicKey(keyBytes));

        await _sut.AddAsync(device);
        _context.ChangeTracker.Clear();

        var loaded = await _sut.GetByIdAsync(device.Id);

        loaded!.Name.Should().Be("Mobile-Scanner-07");
        loaded.PublicKey.Bytes.Should().StartWith(keyBytes);
        loaded.Status.Should().Be(ExamShield.Domain.Enums.DeviceStatus.Pending); // newly registered = Pending
        loaded.IsActive.Should().BeFalse(); // IsActive is false until Approved
        loaded.RegisteredAt.Should().BeCloseTo(device.RegisteredAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(DeviceId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_MultipleDevices_EachRetrievableById()
    {
        var a = MakeDevice("A");
        var b = MakeDevice("B");

        await _sut.AddAsync(a);
        await _sut.AddAsync(b);

        (await _sut.GetByIdAsync(a.Id)).Should().NotBeNull();
        (await _sut.GetByIdAsync(b.Id)).Should().NotBeNull();
    }
}
