using ExamShield.Application.Queries.VerifyAuditChain;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.VerifyAuditChain;

public sealed class VerifyAuditChainQueryHandlerTests
{
    private readonly IAuditLogRepository _repo = Substitute.For<IAuditLogRepository>();
    private readonly VerifyAuditChainQueryHandler _sut;

    public VerifyAuditChainQueryHandlerTests() => _sut = new(_repo);

    private static IReadOnlyList<AuditLog> BuildChain(int count)
    {
        var entries = new List<AuditLog>();
        var prev = string.Empty;
        for (var i = 0; i < count; i++)
        {
            var entry = AuditLog.Record(AuditAction.CaptureRegistered);
            var hash = AuditChainHasher.ComputeContentHash(entry, prev);
            entry.SetChainHashes(prev, hash);
            entries.Add(entry);
            prev = hash;
        }
        return entries;
    }

    [Fact]
    public async Task Handle_EmptyChain_ReturnsValidWithZeroEntries()
    {
        _repo.GetChainAsync(Arg.Any<CaptureId>(), default)
            .Returns(Array.Empty<AuditLog>() as IReadOnlyList<AuditLog>);

        var result = await _sut.Handle(new(Guid.NewGuid()), default);

        result.IsValid.Should().BeTrue();
        result.EntryCount.Should().Be(0);
        result.FirstBrokenIndex.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidChain_ReturnsValidResult()
    {
        var chain = BuildChain(3);
        _repo.GetChainAsync(Arg.Any<CaptureId>(), default).Returns(chain);

        var result = await _sut.Handle(new(Guid.NewGuid()), default);

        result.IsValid.Should().BeTrue();
        result.EntryCount.Should().Be(3);
        result.FirstBrokenIndex.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TamperedEntry_ReturnsInvalidWithBrokenIndex()
    {
        var chain = BuildChain(3).ToList();
        // Corrupt middle entry's hash
        chain[1].SetChainHashes("wrong-prev-hash", chain[1].ContentHash);
        _repo.GetChainAsync(Arg.Any<CaptureId>(), default).Returns(chain);

        var result = await _sut.Handle(new(Guid.NewGuid()), default);

        result.IsValid.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SingleValidEntry_IsValid()
    {
        var chain = BuildChain(1);
        _repo.GetChainAsync(Arg.Any<CaptureId>(), default).Returns(chain);

        var result = await _sut.Handle(new(Guid.NewGuid()), default);

        result.IsValid.Should().BeTrue();
        result.EntryCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_LastEntryTampered_ReturnsBreakAtLastIndex()
    {
        var chain = BuildChain(4).ToList();
        chain[3].SetChainHashes(chain[3].PreviousHash, "wrong-content-hash");
        _repo.GetChainAsync(Arg.Any<CaptureId>(), default).Returns(chain);

        var result = await _sut.Handle(new(Guid.NewGuid()), default);

        result.IsValid.Should().BeFalse();
        result.FirstBrokenIndex.Should().Be(3);
    }
}
