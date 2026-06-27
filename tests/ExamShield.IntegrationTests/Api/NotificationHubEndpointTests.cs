using System.Net;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class NotificationHubEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task HubEndpoint_WithoutWebSocketUpgrade_Returns400()
    {
        // SignalR negotiate handshake without WebSocket headers returns 400 Bad Request
        using var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HubEndpoint_Unauthenticated_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
