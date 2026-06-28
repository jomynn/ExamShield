using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class PublicVerifyTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _auth = null!;

    public async Task InitializeAsync() =>
        _auth = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _auth.Dispose(); return Task.CompletedTask; }

    private async Task<(Guid captureId, byte[] imageBytes)> RegisterAndUploadCapture()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubKey = ecdsa.ExportSubjectPublicKeyInfo();
        var devResp = await _auth.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("PubVerifyDev", pubKey));
        var dev = await devResp.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _auth.PutAsync($"/devices/{dev!.DeviceId}/approve", null);

        var imageBytes = new byte[] { 10, 20, 30, 40, 50 };
        var hash = SHA256.HashData(imageBytes);
        var sig = ecdsa.SignHash(hash);

        var capResp = await _auth.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, factory.EnrollStudentDirectly(factory.ActiveExamId), dev!.DeviceId, 1,
                Convert.ToHexString(hash), sig));
        var cap = await capResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();

        await _auth.PostAsJsonAsync("/upload",
            new UploadImageRequest(cap!.CaptureId, imageBytes));

        return (cap.CaptureId, imageBytes);
    }

    [Fact]
    public async Task GetPublicVerify_WithValidCapture_ReturnsOk()
    {
        var (captureId, _) = await RegisterAndUploadCapture();

        var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?captureId={captureId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPublicVerify_WithValidCapture_ReturnsVerified()
    {
        var (captureId, _) = await RegisterAndUploadCapture();

        var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?captureId={captureId}");
        var body = await response.Content.ReadFromJsonAsync<PublicVerifyResponse>();

        body!.IsValid.Should().BeTrue();
        body.HashValid.Should().BeTrue();
        body.CaptureId.Should().Be(captureId);
    }

    [Fact]
    public async Task GetPublicVerify_WithUnknownCapture_Returns404()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?captureId={Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPublicVerify_WithoutCaptureId_Returns400()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/public/verify");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPublicVerify_DoesNotRequireAuthentication()
    {
        var (captureId, _) = await RegisterAndUploadCapture();

        // Call with no token
        var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/public/verify?captureId={captureId}");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
