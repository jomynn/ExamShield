using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class AnswerSheetImageEndpointTests
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public AnswerSheetImageEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();
        var res = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("ImageTest Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var body = await res.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId = body!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _client.Dispose(); return Task.CompletedTask; }

    private static readonly byte[] SampleImage = "jpeg-answer-sheet-bytes"u8.ToArray();
    private static readonly string SampleHash =
        Convert.ToHexString(SHA256.HashData(SampleImage)).ToLower();

    private async Task<Guid> RegisterCaptureAsync()
    {
        var studentId = _factory.EnrollStudentDirectly(_factory.ActiveExamId);
        var req = new RegisterCaptureRequest(
            _factory.ActiveExamId, studentId, _deviceId, 1,
            SampleHash, _ecdsa.SignHash(Convert.FromHexString(SampleHash)));
        var res = await _client.PostAsJsonAsync("/capture", req);
        var body = await res.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return body!.CaptureId;
    }

    [Fact]
    public async Task GetImage_ForUploadedCapture_ReturnsBytes()
    {
        var captureId = await RegisterCaptureAsync();
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(captureId, SampleImage));

        var res = await _client.GetAsync($"/captures/{captureId}/image");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetImage_ForNonExistentCapture_Returns404()
    {
        var res = await _client.GetAsync($"/captures/{Guid.NewGuid()}/image");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetImage_ForCaptureWithoutUpload_Returns404()
    {
        var captureId = await RegisterCaptureAsync();
        var res = await _client.GetAsync($"/captures/{captureId}/image");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
