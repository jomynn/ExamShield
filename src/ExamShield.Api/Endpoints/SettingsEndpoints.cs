using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.TestAlert;
using ExamShield.Application.Commands.UpdateNotificationSettings;
using ExamShield.Application.Commands.UpdateSettings;
using ExamShield.Application.Queries.GetNotificationSettings;
using ExamShield.Application.Queries.GetSettings;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/settings").WithTags("Settings");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetSettingsQuery(), ct);
            return Results.Ok(ToResponse(dto));
        })
        .WithName("GetSettings")
        .RequireAuthorization("Administrator")
        .Produces<SettingsResponse>();

        group.MapPut("/", async (UpdateSettingsRequest request, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new UpdateSettingsCommand(
                request.OcrConfidenceThreshold, request.NotificationsEnabled,
                request.NotificationSeverity, request.AccessTokenExpiryMinutes,
                request.RefreshTokenExpiryDays), ct);
            return Results.Ok(ToResponse(dto));
        })
        .WithName("UpdateSettings")
        .RequireAuthorization("Administrator")
        .Produces<SettingsResponse>()
        .ProducesValidationProblem();

        group.MapPost("/alert/test", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new TestAlertCommand(), ct);
            return result.Sent
                ? Results.Ok(new AlertTestResponse(true, null))
                : Results.Ok(new AlertTestResponse(false, result.Error));
        })
        .WithName("TestAlert")
        .RequireAuthorization("Administrator")
        .Produces<AlertTestResponse>();

        group.MapGet("/notifications", async (ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetNotificationSettingsQuery(), ct);
            return Results.Ok(ToNotificationResponse(dto));
        })
        .WithName("GetNotificationSettings")
        .RequireAuthorization("Administrator")
        .Produces<NotificationChannelSettingsResponse>();

        group.MapPut("/notifications", async (UpdateNotificationChannelSettingsRequest request, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var dto = await sender.Send(new UpdateNotificationSettingsCommand(
                    request.EmailEnabled,   request.EmailRecipients,
                    request.SlackEnabled,   request.SlackWebhookUrl,
                    request.LineEnabled,    request.LineNotifyToken,
                    request.WebhookEnabled, request.WebhookUrl), ct);
                return Results.Ok(ToNotificationResponse(dto));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { title = ex.Message, status = 400 });
            }
        })
        .WithName("UpdateNotificationSettings")
        .RequireAuthorization("Administrator")
        .Produces<NotificationChannelSettingsResponse>();

        return app;
    }

    private static NotificationChannelSettingsResponse ToNotificationResponse(NotificationSettingsDto dto) =>
        new(dto.EmailEnabled, dto.EmailRecipients, dto.SlackEnabled, dto.SlackWebhookUrl,
            dto.LineEnabled, dto.LineNotifyToken, dto.WebhookEnabled, dto.WebhookUrl, dto.UpdatedAt);

    private static SettingsResponse ToResponse(SettingsDto dto) =>
        new(dto.OcrConfidenceThreshold, dto.NotificationsEnabled,
            dto.NotificationSeverity, dto.AccessTokenExpiryMinutes, dto.RefreshTokenExpiryDays);
}
