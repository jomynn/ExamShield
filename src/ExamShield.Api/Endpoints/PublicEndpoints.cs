using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.PublicVerifyCapture;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/public/verify", PublicVerifyAsync)
            .WithName("PublicVerify")
            .WithTags("Public")
            .AllowAnonymous()
            .Produces<PublicVerifyResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> PublicVerifyAsync(
        Guid? captureId, string? hashHex, ISender sender, CancellationToken ct)
    {
        if (captureId is null && string.IsNullOrWhiteSpace(hashHex))
            return Results.BadRequest("Either captureId or hashHex query parameter is required.");

        if (captureId is not null && !string.IsNullOrWhiteSpace(hashHex))
            return Results.BadRequest("Provide captureId or hashHex, not both.");

        var result = await sender.Send(new PublicVerifyCaptureQuery(captureId, hashHex), ct);

        return Results.Ok(new PublicVerifyResponse(
            result.CaptureId, result.IsValid, result.HashValid,
            result.SignatureValid, result.IsUploaded, result.CapturedAt));
    }
}
