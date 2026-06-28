using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class PublicVerifyByHashTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("HashVerify-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        devRes.EnsureSuccessStatusCode();
        _deviceId = (await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>())!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _client.Dispose(); return Task.CompletedTask; }

    private async Task<(Guid CaptureId, string HashHex)> RegisterCaptureAsync()
    {
        var hashHex = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();
        var capRes  = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, factory.EnrollStudentDirectly(factory.ActiveExamId), _deviceId, 1, hashHex,
                _ecdsa.SignHash(Convert.FromHexString(hashHex))));
        capRes.EnsureSuccessStatusCode();
        var cap = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return (cap!.CaptureId, hashHex);
    }

    [Fact]
    public async Task PublicVerify_ByHashHex_ReturnsVerificationResult()
    {
        var (captureId, hashHex) = await RegisterCaptureAsync();

        var anon     = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?hashHex={hashHex}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PublicVerifyBody>();
        Assert.NotNull(body);
        Assert.Equal(captureId, body!.CaptureId);
    }

    [Fact]
    public async Task PublicVerify_ByCaptureId_StillWorks()
    {
        var (captureId, _) = await RegisterCaptureAsync();

        var anon     = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?captureId={captureId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PublicVerifyBody>();
        Assert.NotNull(body);
        Assert.Equal(captureId, body!.CaptureId);
    }

    [Fact]
    public async Task PublicVerify_UnknownHash_Returns404()
    {
        var anon     = factory.CreateClient();
        var hashHex  = new string('0', 64);
        var response = await anon.GetAsync($"/public/verify?hashHex={hashHex}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublicVerify_NoParams_Returns400()
    {
        var anon     = factory.CreateClient();
        var response = await anon.GetAsync("/public/verify");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record PublicVerifyBody(
        Guid CaptureId, bool IsValid, bool HashValid, bool SignatureValid, bool IsUploaded);
}
