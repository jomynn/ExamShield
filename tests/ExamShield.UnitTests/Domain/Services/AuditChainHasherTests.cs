using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Services;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Services;

public sealed class AuditChainHasherTests
{
    [Fact]
    public void ComputeContentHash_IsDeterministic()
    {
        var entry = AuditLog.Record(AuditAction.CaptureRegistered);
        var prev = "abc123";

        var h1 = AuditChainHasher.ComputeContentHash(entry, prev);
        var h2 = AuditChainHasher.ComputeContentHash(entry, prev);

        h1.Should().Be(h2);
    }

    [Fact]
    public void ComputeContentHash_DifferentPreviousHash_ProducesDifferentResult()
    {
        var entry = AuditLog.Record(AuditAction.CaptureRegistered);

        var h1 = AuditChainHasher.ComputeContentHash(entry, "prev-a");
        var h2 = AuditChainHasher.ComputeContentHash(entry, "prev-b");

        h1.Should().NotBe(h2);
    }

    [Fact]
    public void ComputeContentHash_DifferentAction_ProducesDifferentResult()
    {
        var entry1 = AuditLog.Record(AuditAction.CaptureRegistered);
        var entry2 = AuditLog.Record(AuditAction.HashVerified);

        var h1 = AuditChainHasher.ComputeContentHash(entry1, "");
        var h2 = AuditChainHasher.ComputeContentHash(entry2, "");

        h1.Should().NotBe(h2);
    }

    [Fact]
    public void ComputeContentHash_EmptyPreviousHash_IsValid()
    {
        var entry = AuditLog.Record(AuditAction.UserLoggedIn);
        var result = AuditChainHasher.ComputeContentHash(entry, string.Empty);
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void ComputeContentHash_OutputIsLowerHex()
    {
        var entry = AuditLog.Record(AuditAction.CaptureRegistered);
        var result = AuditChainHasher.ComputeContentHash(entry, "");
        result.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeContentHash_ChainIntegrity_FirstEntryPrevIsEmpty()
    {
        var entry = AuditLog.Record(AuditAction.CaptureRegistered);
        var hash = AuditChainHasher.ComputeContentHash(entry, string.Empty);
        entry.SetChainHashes(string.Empty, hash);

        // Verify the chain validates as expected
        var recomputed = AuditChainHasher.ComputeContentHash(entry, entry.PreviousHash);
        recomputed.Should().Be(entry.ContentHash);
    }

    [Fact]
    public void ComputeContentHash_MultiEntryChain_EachLinkValid()
    {
        var prev = string.Empty;
        var entries = new List<AuditLog>();

        for (var i = 0; i < 5; i++)
        {
            var entry = AuditLog.Record(AuditAction.CaptureRegistered);
            var hash = AuditChainHasher.ComputeContentHash(entry, prev);
            entry.SetChainHashes(prev, hash);
            entries.Add(entry);
            prev = hash;
        }

        // Verify each link independently
        var expectedPrev = string.Empty;
        foreach (var e in entries)
        {
            var expectedHash = AuditChainHasher.ComputeContentHash(e, expectedPrev);
            e.ContentHash.Should().Be(expectedHash);
            e.PreviousHash.Should().Be(expectedPrev);
            expectedPrev = e.ContentHash;
        }
    }
}
