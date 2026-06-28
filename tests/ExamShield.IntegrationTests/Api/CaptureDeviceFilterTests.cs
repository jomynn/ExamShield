using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureDeviceFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _deviceAId;
    private Guid _deviceBId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        // Register two devices and approve both
        using var ecdsaA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var ecdsaB = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var devA = await (await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("FilterDevA", ecdsaA.ExportSubjectPublicKeyInfo())))
            .Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceAId = devA!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceAId}/approve", null);

        var devB = await (await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("FilterDevB", ecdsaB.ExportSubjectPublicKeyInfo())))
            .Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceBId = devB!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceBId}/approve", null);

        // Register one capture per device (exam is already active via factory)
        var examId = factory.ActiveExamId;

        byte[] MakeCapture(ECDsa key, Guid devId)
        {
            var bytes = new byte[] { 1, 2, 3, (byte)(devId.GetHashCode() & 0xFF) };
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        var hashA = MakeCapture(ecdsaA, _deviceAId);
        var sigA  = ecdsaA.SignHash(hashA);
        var studentAId = factory.EnrollStudentDirectly(examId);
        await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(examId, studentAId, _deviceAId, 1,
                Convert.ToHexString(hashA), sigA));

        var hashB = MakeCapture(ecdsaB, _deviceBId);
        var sigB  = ecdsaB.SignHash(hashB);
        var studentBId = factory.EnrollStudentDirectly(examId);
        await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(examId, studentBId, _deviceBId, 1,
                Convert.ToHexString(hashB), sigB));
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetCaptures_WithDeviceIdFilter_ReturnsOnlyThatDevice()
    {
        var res  = await _client.GetAsync($"/captures?deviceId={_deviceAId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        Assert.NotEmpty(body!.Captures);
        Assert.All(body.Captures, c => Assert.Equal(_deviceAId, c.DeviceId));
    }

    [Fact]
    public async Task GetCaptures_DeviceIdFilterExcludesOtherDevices()
    {
        var res  = await _client.GetAsync($"/captures?deviceId={_deviceAId}");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        Assert.DoesNotContain(body!.Captures, c => c.DeviceId == _deviceBId);
    }

    [Fact]
    public async Task GetCaptures_UnknownDeviceId_ReturnsEmptyList()
    {
        var res  = await _client.GetAsync($"/captures?deviceId={Guid.NewGuid()}");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        Assert.Empty(body!.Captures);
    }

    [Fact]
    public async Task GetCaptures_NoDeviceFilter_ReturnsBothDevices()
    {
        var res  = await _client.GetAsync("/captures");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        var ids  = body!.Captures.Select(c => c.DeviceId).ToHashSet();
        Assert.Contains(_deviceAId, ids);
        Assert.Contains(_deviceBId, ids);
    }
}
