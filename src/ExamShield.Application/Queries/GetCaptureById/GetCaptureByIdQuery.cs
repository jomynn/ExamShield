using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetCaptureById;

public sealed record GetCaptureByIdResult(
    Guid CaptureId, Guid ExamId, Guid StudentId, Guid DeviceId,
    int PageNumber, string Hash, string Signature,
    string Status, DateTimeOffset CapturedAt, string? StorageKey);

public sealed record GetCaptureByIdQuery(Guid CaptureId) : IRequest<GetCaptureByIdResult?>;

public sealed class GetCaptureByIdQueryHandler(ICaptureRepository captures)
    : IRequestHandler<GetCaptureByIdQuery, GetCaptureByIdResult?>
{
    public async Task<GetCaptureByIdResult?> Handle(GetCaptureByIdQuery query, CancellationToken ct)
    {
        var capture = await captures.GetByIdAsync(new CaptureId(query.CaptureId), ct);
        if (capture is null) return null;

        return new GetCaptureByIdResult(
            capture.Id.Value, capture.ExamId.Value, capture.StudentId.Value,
            capture.DeviceId.Value, capture.PageNumber.Value,
            capture.ExpectedHash.Hex,
            Convert.ToBase64String(capture.Signature.Bytes.ToArray()),
            capture.Status.ToString(), capture.CapturedAt, capture.StorageKey);
    }
}
