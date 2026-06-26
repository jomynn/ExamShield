using ExamShield.Infrastructure.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace ExamShield.UnitTests.Infrastructure.Cache;

public sealed class CacheServiceTests
{
    private readonly IDistributedCache _inner = Substitute.For<IDistributedCache>();
    private readonly CacheService _sut;

    public CacheServiceTests() => _sut = new CacheService(_inner);

    [Fact]
    public async Task GetAsync_WhenKeyMissing_ReturnsNull()
    {
        _inner.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var result = await _sut.GetAsync<string>("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenKeyPresent_ReturnsDeserializedValue()
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes("hello");
        _inner.GetAsync("key", Arg.Any<CancellationToken>()).Returns(payload);

        var result = await _sut.GetAsync<string>("key");

        result.Should().Be("hello");
    }

    [Fact]
    public async Task SetAsync_SerializesValueAndPassesTtl()
    {
        var ttl = TimeSpan.FromMinutes(5);

        await _sut.SetAsync("key", 42, ttl);

        await _inner.Received(1).SetAsync(
            "key",
            Arg.Is<byte[]>(b => JsonSerializer.Deserialize<int>(b) == 42),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == ttl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_RemovesKey()
    {
        await _sut.InvalidateAsync("key");

        await _inner.Received(1).RemoveAsync("key", Arg.Any<CancellationToken>());
    }
}
