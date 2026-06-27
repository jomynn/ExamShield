using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetCaptures;

public sealed class GetCapturesQueryHandler(ICaptureRepository captures)
    : IRequestHandler<GetCapturesQuery, GetCapturesResult>
{
    public async Task<GetCapturesResult> Handle(GetCapturesQuery request, CancellationToken ct)
    {
        PaginationGuard.Validate(request.Page, request.PageSize);

        var examId    = request.ExamId.HasValue    ? new ExamId(request.ExamId.Value)       : (ExamId?)null;
        var deviceId  = request.DeviceId.HasValue  ? new DeviceId(request.DeviceId.Value)   : (DeviceId?)null;
        var studentId = request.StudentId.HasValue ? new StudentId(request.StudentId.Value) : (StudentId?)null;

        var (items, total) = await captures.ListPagedAsync(
            request.Page, request.PageSize, examId, request.Status, deviceId, studentId, ct);

        var dtos = items
            .Select(c => new CaptureDto(
                c.Id.Value, c.ExamId.Value, c.StudentId.Value, c.DeviceId.Value,
                c.Status.ToString(), c.CapturedAt, c.StorageKey))
            .ToList();

        return new GetCapturesResult(dtos, total, request.Page, request.PageSize);
    }
}
