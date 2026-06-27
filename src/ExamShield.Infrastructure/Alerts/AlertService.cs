using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamShield.Infrastructure.Alerts;

public sealed class AlertService : IAlertService
{
    private readonly IReadOnlyList<IAlertChannel> _staticChannels;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public AlertService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory       = scopeFactory;
        _httpClientFactory  = httpClientFactory;

        var configs = configuration
            .GetSection("Alerts:Channels")
            .Get<AlertChannelConfig[]>() ?? [];

        _staticChannels = configs
            .Where(c => c.Enabled)
            .Select(c => CreateChannel(c, httpClientFactory))
            .Where(ch => ch is not null)
            .Select(ch => ch!)
            .ToList();
    }

    public async Task SendAsync(AlertType type, string message, CancellationToken ct = default)
    {
        var staticTask = Task.WhenAll(_staticChannels.Select(ch => ch.SendAsync(type, message, ct)));

        // Create a fresh scope so we don't capture the Scoped repository as a Singleton.
        using var scope = _scopeFactory.CreateScope();
        var repo    = scope.ServiceProvider.GetRequiredService<INotificationChannelSettingsRepository>();
        var dynamic = new DynamicAlertService(repo, new HttpAlertChannelFactory(_httpClientFactory));

        await Task.WhenAll(staticTask, dynamic.SendAsync(type, message, ct));
    }

    private static IAlertChannel? CreateChannel(AlertChannelConfig config, IHttpClientFactory factory)
    {
        var client = factory.CreateClient("Alerts");
        return config.Type switch
        {
            "Slack" when !string.IsNullOrWhiteSpace(config.Url)
                => new SlackAlertChannel(client, config.Url),

            "Teams" when !string.IsNullOrWhiteSpace(config.Url)
                => new TeamsAlertChannel(client, config.Url),

            "LineNotify" when !string.IsNullOrWhiteSpace(config.Token)
                => new LineNotifyAlertChannel(client, config.Token),

            "Email" when !string.IsNullOrWhiteSpace(config.SmtpHost)
                       && !string.IsNullOrWhiteSpace(config.EmailFrom)
                       && !string.IsNullOrWhiteSpace(config.EmailTo)
                => new EmailAlertChannel(
                    new MailKitSmtpEmailSender(
                        config.SmtpHost, config.SmtpPort,
                        config.SmtpUsername, config.SmtpPassword, config.SmtpUseSsl),
                    config.EmailFrom, config.EmailTo),

            "Webhook" or _ when !string.IsNullOrWhiteSpace(config.Url)
                => new WebhookAlertChannel(client, config.Url),

            _ => null
        };
    }
}

public sealed class AlertChannelConfig
{
    public string Type { get; set; } = "Webhook";
    public bool Enabled { get; set; }

    // HTTP-based channels (Slack, Teams, Webhook)
    public string Url { get; set; } = string.Empty;

    // LINE Notify
    public string Token { get; set; } = string.Empty;

    // Email
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool SmtpUseSsl { get; set; } = true;
    public string EmailFrom { get; set; } = string.Empty;
    public string EmailTo { get; set; } = string.Empty;
}
