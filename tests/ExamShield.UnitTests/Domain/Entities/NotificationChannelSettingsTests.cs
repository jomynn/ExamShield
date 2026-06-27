using ExamShield.Domain.Entities;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class NotificationChannelSettingsTests
{
    [Fact]
    public void CreateDefault_AllChannelsDisabled()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.False(s.EmailEnabled);
        Assert.False(s.SlackEnabled);
        Assert.False(s.LineEnabled);
        Assert.False(s.WebhookEnabled);
    }

    [Fact]
    public void Update_StoresAllFields()
    {
        var s = NotificationChannelSettings.CreateDefault();

        s.Update(
            emailEnabled: true,    emailRecipients: "a@b.com,c@d.com",
            slackEnabled: true,    slackWebhookUrl: "https://hooks.slack.com/T/X",
            lineEnabled: false,    lineNotifyToken: null,
            webhookEnabled: true,  webhookUrl: "https://my.api/notify");

        Assert.True(s.EmailEnabled);
        Assert.Equal("a@b.com,c@d.com", s.EmailRecipients);
        Assert.True(s.SlackEnabled);
        Assert.Equal("https://hooks.slack.com/T/X", s.SlackWebhookUrl);
        Assert.False(s.LineEnabled);
        Assert.True(s.WebhookEnabled);
        Assert.Equal("https://my.api/notify", s.WebhookUrl);
    }

    [Fact]
    public void Update_SlackEnabledWithoutUrl_ThrowsArgumentException()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            s.Update(false, null, slackEnabled: true, slackWebhookUrl: null, false, null, false, null));
    }

    [Fact]
    public void Update_WebhookEnabledWithoutUrl_ThrowsArgumentException()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            s.Update(false, null, false, null, false, null, webhookEnabled: true, webhookUrl: null));
    }

    [Fact]
    public void Update_EmailEnabledWithoutRecipients_ThrowsArgumentException()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            s.Update(emailEnabled: true, emailRecipients: null, false, null, false, null, false, null));
    }

    [Fact]
    public void Update_InvalidSlackUrl_ThrowsArgumentException()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            s.Update(false, null, slackEnabled: true, slackWebhookUrl: "not-a-url", false, null, false, null));
    }

    [Fact]
    public void Update_InvalidWebhookUrl_ThrowsArgumentException()
    {
        var s = NotificationChannelSettings.CreateDefault();

        Assert.Throws<ArgumentException>(() =>
            s.Update(false, null, false, null, false, null, webhookEnabled: true, webhookUrl: "ftp://bad-scheme"));
    }
}
