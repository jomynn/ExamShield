using ExamShield.Api.Contracts;
using ExamShield.Api.RateLimiting;
using ExamShield.Application.Commands.UploadImage;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class UploadEndpoints
{
    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/upload", UploadImageAsync)
            .WithName("UploadImage")
            .WithTags("Upload")
            .RequireAuthorization("Invigilator")
            .RequireRateLimiting(RateLimitingExtensions.UploadPolicy)
            .Produces<UploadImageResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> UploadImageAsync(
        UploadImageRequest request, ISender sender, CancellationToken ct)
    {
        var command = new UploadImageCommand(request.CaptureId, request.ImageBytes);
        var result = await sender.Send(command, ct);

        return Results.Created(
            $"/capture/{request.CaptureId}",
            new UploadImageResponse(result.StorageKey));
    }
}
