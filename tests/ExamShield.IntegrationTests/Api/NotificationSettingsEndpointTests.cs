using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class NotificationSettingsEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetNotificationSettings_ReturnsDefaultAllDisabled()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var res = await client.GetAsync("/settings/notifications");

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<NotificationChannelSettingsResponse>();
        Assert.NotNull(body);
        Assert.False(body.EmailEnabled);
        Assert.False(body.SlackEnabled);
        Assert.False(body.LineEnabled);
        Assert.False(body.WebhookEnabled);
    }

    [Fact]
    public async Task UpdateNotificationSettings_ValidPayload_Returns200WithUpdatedValues()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var request = new UpdateNotificationChannelSettingsRequest(
            EmailEnabled:   true,  EmailRecipients:  "ops@examshield.local",
            SlackEnabled:   true,  SlackWebhookUrl:  "https://hooks.slack.com/T/XYZ",
            LineEnabled:    false, LineNotifyToken:  null,
            WebhookEnabled: false, WebhookUrl:       null);

        var res = await client.PutAsJsonAsync("/settings/notifications", request);

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<NotificationChannelSettingsResponse>();
        Assert.NotNull(body);
        Assert.True(body.EmailEnabled);
        Assert.Equal("ops@examshield.local", body.EmailRecipients);
        Assert.True(body.SlackEnabled);
        Assert.Equal("https://hooks.slack.com/T/XYZ", body.SlackWebhookUrl);
        Assert.False(body.LineEnabled);
    }

    [Fact]
    public async Task UpdateNotificationSettings_SlackEnabledWithoutUrl_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var request = new UpdateNotificationChannelSettingsRequest(
            false, null, SlackEnabled: true, SlackWebhookUrl: null, false, null, false, null);

        var res = await client.PutAsJsonAsync("/settings/notifications", request);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UpdateNotificationSettings_InvalidWebhookUrl_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var request = new UpdateNotificationChannelSettingsRequest(
            false, null, false, null, false, null, WebhookEnabled: true, WebhookUrl: "not-a-url");

        var res = await client.PutAsJsonAsync("/settings/notifications", request);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetNotificationSettings_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync("/settings/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UpdateNotificationSettings_PersistsAcrossGetRequest()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var request = new UpdateNotificationChannelSettingsRequest(
            false, null, false, null, LineEnabled: true, LineNotifyToken: "my-line-token", false, null);

        await client.PutAsJsonAsync("/settings/notifications", request);
        var res  = await client.GetAsync("/settings/notifications");
        var body = await res.Content.ReadFromJsonAsync<NotificationChannelSettingsResponse>();

        Assert.NotNull(body);
        Assert.True(body.LineEnabled);
        Assert.Equal("my-line-token", body.LineNotifyToken);
    }
}
