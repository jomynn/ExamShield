using ExamShield.Application.Queries.ExportCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class ExportCapturesQueryHandlerTests
{
    private readonly ICaptureRepository _repo = Substitute.For<ICaptureRepository>();
    private readonly ExportCapturesQueryHandler _sut;

    public ExportCapturesQueryHandlerTests() => _sut = new ExportCapturesQueryHandler(_repo);

    private static Capture MakeCapture(ExamId examId)
    {
        var hash = Hash.FromBytes(new byte[32]);
        return Capture.Create(examId, StudentId.New(), DeviceId.New(),
            new PageNumber(1), hash, new Signature(new byte[64]));
    }

    [Fact]
    public async Task Handle_WithNoFilter_ExportsCsvWithHeader()
    {
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new ExportCapturesQuery(), default);

        Assert.Contains("CaptureId", result.Csv);
        Assert.Contains("ExamId", result.Csv);
        Assert.Contains("StudentId", result.Csv);
        Assert.Contains("Status", result.Csv);
    }

    [Fact]
    public async Task Handle_WithCaptures_IncludesEachRowInCsv()
    {
        var examId = ExamId.New();
        var c1 = MakeCapture(examId);
        var c2 = MakeCapture(examId);
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { c1, c2 });

        var result = await _sut.Handle(new ExportCapturesQuery(), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 rows
    }

    [Fact]
    public async Task Handle_WithExamIdFilter_UsesListByExamIdAsync()
    {
        var examId = ExamId.New();
        _repo.ListByExamIdAsync(examId, Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCapture(examId) });

        var result = await _sut.Handle(new ExportCapturesQuery(ExamId: examId.Value), default);

        await _repo.Received(1).ListByExamIdAsync(examId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().ListAllAsync(Arg.Any<CancellationToken>());
        Assert.NotEmpty(result.Csv);
    }

    [Fact]
    public async Task Handle_FilenameContainsCapturesPrefix()
    {
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new ExportCapturesQuery(), default);

        Assert.StartsWith("captures-", result.Filename);
        Assert.EndsWith(".csv", result.Filename);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_FiltersCaptures()
    {
        var examId = ExamId.New();
        var uploaded = MakeCapture(examId);
        uploaded.RecordUpload("storage/key");
        var created = MakeCapture(examId);
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new[] { uploaded, created });

        var result = await _sut.Handle(
            new ExportCapturesQuery(Status: CaptureStatus.Uploaded), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + 1 matching row
    }
}
