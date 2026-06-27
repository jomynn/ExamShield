using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class AlertTestEndpointTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AlertTestEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync() =>
        _client = await _factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TestAlert_Authenticated_Returns200()
    {
        var res = await _client.PostAsync("/settings/alert/test", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task TestAlert_ResponseHasSentField()
    {
        var res = await _client.PostAsync("/settings/alert/test", null);
        var body = await res.Content.ReadFromJsonAsync<AlertTestResponse>();

        Assert.NotNull(body);
        Assert.True(body!.Sent);
    }

    [Fact]
    public async Task TestAlert_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsync("/settings/alert/test", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
