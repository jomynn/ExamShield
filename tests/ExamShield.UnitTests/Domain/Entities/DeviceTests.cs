using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Events;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class DeviceTests
{
    private static readonly PublicKey TestKey = new(new byte[] { 0x04, 0x01 });

    [Fact]
    public void Register_CreatesDeviceWithGivenName()
    {
        var device = Device.Register("Scanner-01", TestKey);

        device.Name.Should().Be("Scanner-01");
    }

    [Fact]
    public void Register_StoresPublicKey()
    {
        var device = Device.Register("Scanner-01", TestKey);

        device.PublicKey.Should().Be(TestKey);
    }

    [Fact]
    public void Register_SetsStatusPending()
    {
        var device = Device.Register("Scanner-01", TestKey);

        device.Status.Should().Be(ExamShield.Domain.Enums.DeviceStatus.Pending);
        device.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Approve_SetsStatusApproved_AndIsActiveTrue()
    {
        var device = Device.Register("Scanner-01", TestKey);
        device.Approve();

        device.Status.Should().Be(ExamShield.Domain.Enums.DeviceStatus.Approved);
        device.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Register_AssignsNonEmptyId()
    {
        var device = Device.Register("Scanner-01", TestKey);

        device.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Register_SetsRegisteredAtToNow()
    {
        var before = DateTimeOffset.UtcNow;

        var device = Device.Register("Scanner-01", TestKey);

        device.RegisteredAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Register_RaisesDeviceRegisteredEvent()
    {
        var device = Device.Register("Scanner-01", TestKey);

        device.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeviceRegistered>();
    }

    [Fact]
    public void Register_WithNullName_ThrowsArgumentException()
    {
        var act = () => Device.Register(null!, TestKey);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithWhitespaceName_ThrowsArgumentException()
    {
        var act = () => Device.Register("   ", TestKey);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithNullPublicKey_ThrowsArgumentNullException()
    {
        var act = () => Device.Register("Scanner-01", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TwoDevices_AlwaysHaveDifferentIds()
    {
        Device.Register("A", TestKey).Id.Should().NotBe(Device.Register("B", TestKey).Id);
    }

    // ── Disable / Enable ──────────────────────────────────────────────────────

    [Fact]
    public void Disable_SetsStatusDisabled()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Approve();
        device.Disable();
        device.Status.Should().Be(DeviceStatus.Disabled);
        device.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Enable_DisabledDevice_SetsStatusApproved()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Disable();
        device.Enable();
        device.Status.Should().Be(DeviceStatus.Approved);
        device.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Enable_BlacklistedDevice_ThrowsInvalidOperation()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Blacklist("Stolen");
        device.Invoking(d => d.Enable()).Should().Throw<InvalidOperationException>().WithMessage("*blacklisted*");
    }

    // ── Blacklist ─────────────────────────────────────────────────────────────

    [Fact]
    public void Blacklist_SetsStatusAndReason()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Blacklist("  Reported stolen  ");

        device.Status.Should().Be(DeviceStatus.Blacklisted);
        device.BlacklistReason.Should().Be("Reported stolen");
    }

    [Fact]
    public void Blacklist_AlreadyBlacklisted_ThrowsInvalidOperation()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Blacklist("First reason");
        device.Invoking(d => d.Blacklist("Second")).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blacklist_EmptyReason_ThrowsArgumentException(string reason)
    {
        var device = Device.Register("Scanner", TestKey);
        var act = () => device.Blacklist(reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Approve_BlacklistedDevice_ThrowsInvalidOperation()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Blacklist("Stolen");
        device.Invoking(d => d.Approve()).Should().Throw<InvalidOperationException>().WithMessage("*blacklisted*");
    }

    // ── RecordHeartbeat ───────────────────────────────────────────────────────

    [Fact]
    public void RecordHeartbeat_ActiveDevice_UpdatesLastSeenAt()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Approve();
        var before = DateTimeOffset.UtcNow;

        device.RecordHeartbeat();

        device.LastSeenAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RecordHeartbeat_DisabledDevice_ThrowsInvalidOperation()
    {
        var device = Device.Register("Scanner", TestKey);
        device.Disable();
        device.Invoking(d => d.RecordHeartbeat()).Should().Throw<InvalidOperationException>().WithMessage("*disabled*");
    }
}
