using ExamShield.Infrastructure.Security;
using FluentAssertions;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class InMemoryTotpUsedCodeCacheTests
{
    private readonly InMemoryTotpUsedCodeCache _sut = new();

    [Fact]
    public async Task IsUsedAsync_NewCode_ReturnsFalse()
    {
        var result = await _sut.IsUsedAsync("user1", "123456");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkUsed_ThenIsUsed_ReturnsTrue()
    {
        await _sut.MarkUsedAsync("user1", "123456");
        var result = await _sut.IsUsedAsync("user1", "123456");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SameCode_DifferentUser_IsNotUsed()
    {
        await _sut.MarkUsedAsync("user1", "111111");
        var result = await _sut.IsUsedAsync("user2", "111111");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SameUser_DifferentCode_IsNotUsed()
    {
        await _sut.MarkUsedAsync("user1", "111111");
        var result = await _sut.IsUsedAsync("user1", "222222");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkUsed_CalledTwiceSameEntry_IsIdempotent()
    {
        await _sut.MarkUsedAsync("user1", "999999");
        await _sut.MarkUsedAsync("user1", "999999"); // should not throw
        var result = await _sut.IsUsedAsync("user1", "999999");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleCodesForSameUser_TrackedIndependently()
    {
        await _sut.MarkUsedAsync("user1", "aaa");
        await _sut.MarkUsedAsync("user1", "bbb");

        (await _sut.IsUsedAsync("user1", "aaa")).Should().BeTrue();
        (await _sut.IsUsedAsync("user1", "bbb")).Should().BeTrue();
        (await _sut.IsUsedAsync("user1", "ccc")).Should().BeFalse();
    }
}
