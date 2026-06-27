using ExamShield.Domain.Entities;
using MediatR;

namespace ExamShield.Application.Queries.GetCaptures;

public sealed record CaptureDto(
    Guid CaptureId, Guid ExamId, Guid StudentId, Guid DeviceId,
    string Status, DateTimeOffset CapturedAt, string? StorageKey);

public sealed record GetCapturesResult(
    IReadOnlyList<CaptureDto> Captures,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public sealed record GetCapturesQuery(
    int Page = 1,
    int PageSize = 50,
    Guid? ExamId = null,
    CaptureStatus? Status = null,
    Guid? DeviceId = null,
    Guid? StudentId = null) : IRequest<GetCapturesResult>;
