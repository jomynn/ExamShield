using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DeviceHeartbeatTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _deviceId;

    public DeviceHeartbeatTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var res = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Heartbeat Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await res.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId = device!.DeviceId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Heartbeat_WithActiveDevice_Returns200WithLastSeenAt()
    {
        var response = await _client.PostAsync($"/devices/{_deviceId}/heartbeat", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.True(body!.LastSeenAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task Heartbeat_WithUnknownDevice_Returns404()
    {
        var response = await _client.PostAsync($"/devices/{Guid.NewGuid()}/heartbeat", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.PostAsync($"/devices/{_deviceId}/heartbeat", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDevices_ShowsLastSeenAt_AfterHeartbeat()
    {
        await _client.PostAsync($"/devices/{_deviceId}/heartbeat", null);
        var listRes = await _client.GetAsync("/devices");
        var body = await listRes.Content.ReadFromJsonAsync<DeviceListResponse>();
        var device = body!.Devices.FirstOrDefault(d => d.DeviceId == _deviceId);

        Assert.NotNull(device);
        Assert.NotNull(device.LastSeenAt);
    }
}
