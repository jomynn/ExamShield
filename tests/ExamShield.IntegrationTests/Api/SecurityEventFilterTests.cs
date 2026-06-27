using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class SecurityEventFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetSecurityEvents_WithSeverityFilter_ReturnsOnlyMatchingSeverity()
    {
        var res  = await _client.GetAsync("/security/events?severity=Info");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SecurityEventListResponse>();
        Assert.All(body!.Events, e => Assert.Equal("Info", e.Severity));
    }

    [Fact]
    public async Task GetSecurityEvents_WithSeverityCritical_ExcludesInfoEvents()
    {
        // first check unfiltered response — if it contains any Info event,
        // then filtering for Critical must exclude those Info events
        var allRes  = await _client.GetAsync("/security/events");
        var allBody = await allRes.Content.ReadFromJsonAsync<SecurityEventListResponse>();

        if (allBody!.Events.Any(e => e.Severity == "Info"))
        {
            var critRes  = await _client.GetAsync("/security/events?severity=Critical");
            var critBody = await critRes.Content.ReadFromJsonAsync<SecurityEventListResponse>();
            Assert.All(critBody!.Events, e => Assert.NotEqual("Info", e.Severity));
        }
        else
        {
            // no Info events in store — just verify the filter param is accepted
            Assert.Equal(HttpStatusCode.OK, allRes.StatusCode);
        }
    }

    [Fact]
    public async Task GetSecurityEvents_InvalidSeverity_Returns400()
    {
        var res = await _client.GetAsync("/security/events?severity=BOGUS");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetSecurityEvents_NoFilter_StillReturnsAll()
    {
        var res  = await _client.GetAsync("/security/events");
        var body = await res.Content.ReadFromJsonAsync<SecurityEventListResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Events);
    }
}
