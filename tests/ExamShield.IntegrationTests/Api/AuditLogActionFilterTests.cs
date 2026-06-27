using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class AuditLogActionFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetAuditLog_WithActionFilter_ReturnsOnlyThatAction()
    {
        var res  = await _client.GetAsync("/audit?action=UserCreated");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<AuditLogResponse>();
        Assert.All(body!.Entries, e => Assert.Equal("UserCreated", e.Action));
    }

    [Fact]
    public async Task GetAuditLog_ActionFilter_ExcludesOtherActions()
    {
        // The factory seeds a user, so UserCreated entries exist.
        // Filter by DeviceRegistered — should exclude UserCreated.
        var res  = await _client.GetAsync("/audit?action=DeviceRegistered");
        var body = await res.Content.ReadFromJsonAsync<AuditLogResponse>();
        Assert.All(body!.Entries, e => Assert.NotEqual("UserCreated", e.Action));
    }

    [Fact]
    public async Task GetAuditLog_InvalidAction_Returns400()
    {
        var res = await _client.GetAsync("/audit?action=BOGUS");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetAuditLog_NoActionFilter_ReturnsAll()
    {
        var res  = await _client.GetAsync("/audit");
        var body = await res.Content.ReadFromJsonAsync<AuditLogResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Entries);
    }

    [Fact]
    public async Task GetAuditLog_CaseInsensitiveAction_Succeeds()
    {
        var res = await _client.GetAsync("/audit?action=usercreated");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
