using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class VerifyEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public VerifyEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var response = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Verify-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
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

    private async Task<(Guid captureId, byte[] imageBytes)> RegisterAndUploadAsync()
    {
        var imageBytes = "answer-sheet-content"u8.ToArray();
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLower();

        var captureResp = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                _factory.ActiveExamId, _factory.EnrollStudentDirectly(_factory.ActiveExamId), _deviceId, 1, hashHex,
                _ecdsa.SignHash(Convert.FromHexString(hashHex))));

        var captureBody = await captureResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        await _client.PostAsJsonAsync("/upload",
            new UploadImageRequest(captureBody!.CaptureId, imageBytes));

        return (captureBody.CaptureId, imageBytes);
    }

    private async Task<Guid> RegisterOnlyAsync()
    {
        var hashHex = new string('c', 64);
        var captureResp = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                _factory.ActiveExamId, _factory.EnrollStudentDirectly(_factory.ActiveExamId), _deviceId, 1, hashHex,
                _ecdsa.SignHash(Convert.FromHexString(hashHex))));

        var body = await captureResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return body!.CaptureId;
    }

    [Fact]
    public async Task GetVerify_AfterUpload_Returns200WithIsValidTrue()
    {
        var (captureId, _) = await RegisterAndUploadAsync();
        var response = await _client.GetAsync($"/verify/{captureId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServerVerifyResponse>();
        body!.IsValid.Should().BeTrue();
        body.HashValid.Should().BeTrue();
        body.SignatureValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetVerify_BeforeUpload_Returns400()
    {
        var captureId = await RegisterOnlyAsync();
        var response = await _client.GetAsync($"/verify/{captureId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetVerify_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/verify/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
