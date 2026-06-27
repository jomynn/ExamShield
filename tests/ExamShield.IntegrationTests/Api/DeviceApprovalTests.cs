using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DeviceApprovalTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<(HttpClient Client, Guid DeviceId)> RegisterPendingDeviceAsync()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var res = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Approval-Test Device", ecdsa.ExportSubjectPublicKeyInfo()));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        return (client, body!.DeviceId);
    }

    [Fact]
    public async Task RegisterDevice_NewDevice_HasPendingStatus()
    {
        var (client, deviceId) = await RegisterPendingDeviceAsync();

        var listRes = await client.GetAsync("/devices");
        var list    = await listRes.Content.ReadFromJsonAsync<DeviceListResponse>();

        var device = list!.Devices.First(d => d.DeviceId == deviceId);
        Assert.Equal("Pending", device.Status);
        Assert.False(device.IsActive);
    }

    [Fact]
    public async Task ApproveDevice_PendingDevice_Returns204()
    {
        var (client, deviceId) = await RegisterPendingDeviceAsync();

        var response = await client.PutAsync($"/devices/{deviceId}/approve", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ApproveDevice_PendingDevice_StatusBecomesApproved()
    {
        var (client, deviceId) = await RegisterPendingDeviceAsync();

        await client.PutAsync($"/devices/{deviceId}/approve", null);

        var listRes = await client.GetAsync("/devices");
        var list    = await listRes.Content.ReadFromJsonAsync<DeviceListResponse>();
        var device  = list!.Devices.First(d => d.DeviceId == deviceId);
        Assert.Equal("Approved", device.Status);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task ApproveDevice_UnknownId_Returns404()
    {
        var client   = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PutAsync($"/devices/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RegisterCapture_WithPendingDevice_Returns422()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var devRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Pending-Capture Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        // deliberately NOT approving

        var hashHex = new string('c', 64);
        var capRes = await client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, Guid.NewGuid(), device!.DeviceId, 1,
                hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, capRes.StatusCode);
    }

    [Fact]
    public async Task ListDevices_ReturnsStatusField()
    {
        var (client, deviceId) = await RegisterPendingDeviceAsync();

        var res  = await client.GetAsync("/devices");
        var list = await res.Content.ReadFromJsonAsync<DeviceListResponse>();

        Assert.All(list!.Devices, d => Assert.NotNull(d.Status));
        var device = list.Devices.First(d => d.DeviceId == deviceId);
        Assert.Equal("Pending", device.Status);
    }
}
