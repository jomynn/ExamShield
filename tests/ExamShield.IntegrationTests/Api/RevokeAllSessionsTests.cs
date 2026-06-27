using System.Net;
using System.Net.Http.Json;

namespace ExamShield.IntegrationTests.Api;

public sealed class RevokeAllSessionsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task DeleteSessions_AuthenticatedUser_Returns204()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var res = await client.DeleteAsync("/auth/sessions");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task DeleteSessions_AfterRevoke_RefreshTokenIsInvalid()
    {
        // Log in to get a refresh token
        var client = factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/auth/login",
            new { email = TestWebApplicationFactory.AdminEmail, password = TestWebApplicationFactory.AdminPassword });
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginBody>();

        // Revoke all sessions
        var authed = await factory.CreateAuthenticatedClientAsync();
        await authed.DeleteAsync("/auth/sessions");

        // Attempt refresh — should now fail
        var refreshRes = await client.PostAsJsonAsync("/auth/refresh",
            new { refreshToken = loginBody!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task DeleteSessions_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var res = await client.DeleteAsync("/auth/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private sealed record LoginBody(string Token, string RefreshToken, string Role, bool RequiresMfa);
}
