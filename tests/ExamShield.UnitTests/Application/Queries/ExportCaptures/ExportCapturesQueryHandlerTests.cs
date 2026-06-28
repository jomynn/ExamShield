using ExamShield.Application.Queries.ExportCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.ExportCaptures;

public sealed class ExportCapturesQueryHandlerTests
{
    private readonly ICaptureRepository _repo = Substitute.For<ICaptureRepository>();
    private readonly ExportCapturesQueryHandler _sut;

    public ExportCapturesQueryHandlerTests() => _sut = new(_repo);

    private static Capture MakeCapture(ExamId? examId = null)
    {
        var exam = examId ?? new ExamId(Guid.NewGuid());
        var hash = Hash.FromBytes(new byte[32]);
        return Capture.Create(exam, new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1), hash, new Signature(new byte[64]));
    }

    [Fact]
    public async Task Handle_NoFilter_CallsListAllAsync()
    {
        IReadOnlyList<Capture> empty = [];
        _repo.ListAllAsync(default).Returns(empty);

        await _sut.Handle(new(), default);

        await _repo.Received(1).ListAllAsync(default);
    }

    [Fact]
    public async Task Handle_WithExamId_CallsListByExamIdAsync()
    {
        var examId = Guid.NewGuid();
        IReadOnlyList<Capture> empty = [];
        _repo.ListByExamIdAsync(Arg.Is<ExamId>(e => e.Value == examId), default).Returns(empty);

        await _sut.Handle(new(ExamId: examId), default);

        await _repo.Received(1).ListByExamIdAsync(Arg.Is<ExamId>(e => e.Value == examId), default);
    }

    [Fact]
    public async Task Handle_CsvIncludesHeader()
    {
        IReadOnlyList<Capture> empty = [];
        _repo.ListAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.Csv.Should().Contain("CaptureId,ExamId,StudentId");
    }

    [Fact]
    public async Task Handle_WithStatusFilter_OnlyIncludesMatchingCaptures()
    {
        var cap = MakeCapture();
        cap.RecordUpload("key");
        IReadOnlyList<Capture> list = [cap, MakeCapture()]; // one uploaded, one created
        _repo.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(Status: CaptureStatus.Uploaded), default);

        result.Csv.Trim().Split('\n').Should().HaveCount(2); // header + 1 row
    }

    [Fact]
    public async Task Handle_FilenameStartsWithCaptures()
    {
        IReadOnlyList<Capture> empty = [];
        _repo.ListAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.Filename.Should().StartWith("captures-");
        result.Filename.Should().EndWith(".csv");
    }
}
