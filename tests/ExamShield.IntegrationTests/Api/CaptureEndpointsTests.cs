using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public CaptureEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var response = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var body = await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId = body!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync()
    {
        _ecdsa.Dispose();
        _client.Dispose();
        return Task.CompletedTask;
    }

    private RegisterCaptureRequest SignedRequest(string hashHex) =>
        new(ExamId: _factory.ActiveExamId, StudentId: Guid.NewGuid(), DeviceId: _deviceId,
            PageNumber: 1, HashHex: hashHex,
            SignatureBytes: _ecdsa.SignHash(Convert.FromHexString(hashHex)));

    private RegisterCaptureRequest SignedRequest() => SignedRequest(new string('a', 64));

    [Fact]
    public async Task PostCapture_WithValidBody_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/capture", SignedRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostCapture_WithValidBody_ReturnsNonEmptyCaptureId()
    {
        var response = await _client.PostAsJsonAsync("/capture", SignedRequest());
        var body = await response.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        body!.CaptureId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PostCapture_WithValidBody_ReturnsLocationHeader()
    {
        var response = await _client.PostAsJsonAsync("/capture", SignedRequest());
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith("/capture/");
    }

    [Fact]
    public async Task PostCapture_WithInvalidHashHex_Returns400()
    {
        var request = SignedRequest() with { HashHex = "not-a-hash" };
        var response = await _client.PostAsJsonAsync("/capture", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCapture_WithZeroPageNumber_Returns400()
    {
        var request = SignedRequest() with { PageNumber = 0 };
        var response = await _client.PostAsJsonAsync("/capture", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCapture_WithEmptySignature_Returns400()
    {
        var request = SignedRequest() with { SignatureBytes = Array.Empty<byte>() };
        var response = await _client.PostAsJsonAsync("/capture", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostCapture_WithUnknownDevice_Returns404()
    {
        var hashHex = new string('a', 64);
        var request = new RegisterCaptureRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostCapture_WithInvalidSignature_Returns400()
    {
        var request = SignedRequest() with { SignatureBytes = new byte[64] };
        var response = await _client.PostAsJsonAsync("/capture", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostVerify_WhenHashMatches_Returns200WithIsValidTrue()
    {
        var imageBytes = "exam-answer-sheet"u8.ToArray();
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

        var registerResponse = await _client.PostAsJsonAsync("/capture", SignedRequest(hashHex));
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterCaptureResponse>();

        var verifyResponse = await _client.PostAsJsonAsync(
            $"/capture/{registered!.CaptureId}/verify",
            new VerifyIntegrityRequest(imageBytes));

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await verifyResponse.Content.ReadFromJsonAsync<VerifyIntegrityResponse>();
        body!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PostVerify_WhenHashMismatches_Returns200WithIsValidFalse()
    {
        var registerResponse = await _client.PostAsJsonAsync("/capture", SignedRequest());
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterCaptureResponse>();

        var verifyResponse = await _client.PostAsJsonAsync(
            $"/capture/{registered!.CaptureId}/verify",
            new VerifyIntegrityRequest("tampered-image"u8.ToArray()));

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await verifyResponse.Content.ReadFromJsonAsync<VerifyIntegrityResponse>();
        body!.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task PostVerify_ForUnknownCaptureId_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/capture/{Guid.NewGuid()}/verify",
            new VerifyIntegrityRequest("any"u8.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
