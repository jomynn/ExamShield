using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class LoginHistoryDateRangeTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetLoginHistory_NoDateFilter_ReturnsAll()
    {
        var res  = await _client.GetAsync("/security/login-history");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginHistoryResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Events);
    }

    [Fact]
    public async Task GetLoginHistory_WithFutureFrom_ReturnsEmpty()
    {
        var from = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var res  = await _client.GetAsync($"/security/login-history?from={Uri.EscapeDataString(from)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<LoginHistoryResponse>();
        Assert.Empty(body!.Events);
    }

    [Fact]
    public async Task GetLoginHistory_WithPastTo_ReturnsEmpty()
    {
        var to  = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var res = await _client.GetAsync($"/security/login-history?to={Uri.EscapeDataString(to)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<LoginHistoryResponse>();
        Assert.Empty(body!.Events);
    }

    [Fact]
    public async Task GetLoginHistory_WithBroadRange_ReturnsLoginEvents()
    {
        // The test factory performs a login during setup — should appear in a wide range.
        var from = DateTimeOffset.UtcNow.AddHours(-1).ToString("O");
        var to   = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        var res  = await _client.GetAsync(
            $"/security/login-history?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<LoginHistoryResponse>();
        Assert.NotEmpty(body!.Events);
    }
}
