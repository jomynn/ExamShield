using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class OcrEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _captureId;

    private static readonly byte[] ImageBytes = "ocr-test-exam-image"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public OcrEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var deviceResponse = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("OCR-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var captureResponse = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: _factory.ActiveExamId, StudentId: _factory.EnrollStudentDirectly(_factory.ActiveExamId),
            DeviceId: device!.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: _ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureResponse.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
    }

    public Task DisposeAsync()
    {
        _ecdsa.Dispose();
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostOcr_WithUploadedCapture_Returns202Accepted()
    {
        var response = await _client.PostAsync($"/ocr/{_captureId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetOcr_AfterTrigger_Returns200WithResult()
    {
        await _client.PostAsync($"/ocr/{_captureId}", null);

        var response = await _client.GetAsync($"/ocr/{_captureId}");
        var body = await response.Content.ReadFromJsonAsync<GetOcrResultResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.OcrResultId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PostOcr_Unauthenticated_Returns401()
    {
        using var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsync($"/ocr/{Guid.NewGuid()}", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
