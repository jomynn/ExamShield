using ExamShield.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class RedisTotpUsedCodeCacheTests
{
    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();
    private readonly RedisTotpUsedCodeCache _sut;

    public RedisTotpUsedCodeCacheTests()
    {
        _sut = new RedisTotpUsedCodeCache(_cache);
    }

    [Fact]
    public async Task IsUsedAsync_WhenKeyAbsent_ReturnsFalse()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var result = await _sut.IsUsedAsync("user1", "123456");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUsedAsync_WhenKeyPresent_ReturnsTrue()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        var result = await _sut.IsUsedAsync("user1", "123456");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkUsedAsync_CallsCacheSetWithCorrectKey()
    {
        await _sut.MarkUsedAsync("user42", "999888");

        await _cache.Received(1).SetAsync(
            "totp:user42:999888",
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsUsedAsync_UsesExpectedCacheKey()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        await _sut.IsUsedAsync("user99", "000111");

        await _cache.Received(1).GetAsync("totp:user99:000111", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkUsedAsync_SetsTtlOf3Minutes()
    {
        DistributedCacheEntryOptions? captured = null;
        await _cache.SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Do<DistributedCacheEntryOptions>(o => captured = o),
            Arg.Any<CancellationToken>());

        await _sut.MarkUsedAsync("user1", "123456");

        captured.Should().NotBeNull();
        captured!.AbsoluteExpirationRelativeToNow.Should().Be(TimeSpan.FromMinutes(3));
    }
}
