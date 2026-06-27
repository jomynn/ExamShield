using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class SecuritySessionsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetAllSessions_ReturnsOk()
    {
        var res = await _client.GetAsync("/security/sessions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetAllSessions_ReturnsSessionList()
    {
        var res  = await _client.GetAsync("/security/sessions");
        var body = await res.Content.ReadFromJsonAsync<AllSessionsResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Sessions);
    }

    [Fact]
    public async Task GetAllSessions_AfterLogin_ContainsAtLeastOneSession()
    {
        var res  = await _client.GetAsync("/security/sessions");
        var body = await res.Content.ReadFromJsonAsync<AllSessionsResponse>();
        Assert.NotEmpty(body!.Sessions);
    }

    [Fact]
    public async Task GetAllSessions_WithUserIdFilter_ReturnsOnlyThatUser()
    {
        // list all sessions to find a userId
        var all  = await (await _client.GetAsync("/security/sessions"))
            .Content.ReadFromJsonAsync<AllSessionsResponse>();
        var firstUserId = all!.Sessions.First().UserId;

        var filtered = await (await _client.GetAsync($"/security/sessions?userId={firstUserId}"))
            .Content.ReadFromJsonAsync<AllSessionsResponse>();
        Assert.All(filtered!.Sessions, s => Assert.Equal(firstUserId, s.UserId));
    }

    [Fact]
    public async Task GetAllSessions_Unauthenticated_Returns401()
    {
        var res = await factory.CreateClient().GetAsync("/security/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
