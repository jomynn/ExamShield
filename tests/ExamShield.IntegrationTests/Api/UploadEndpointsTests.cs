using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class UploadEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public UploadEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var response = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Upload-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
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

    private static readonly byte[] SampleImage = "answer-sheet-bytes"u8.ToArray();
    private static readonly string SampleHashHex =
        Convert.ToHexString(SHA256.HashData(SampleImage)).ToLower();

    private async Task<Guid> RegisterCaptureAsync(string? hashHex = null)
    {
        hashHex ??= SampleHashHex;
        var request = new RegisterCaptureRequest(
            _factory.ActiveExamId, Guid.NewGuid(), _deviceId, 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);
        var body = await response.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return body!.CaptureId;
    }

    [Fact]
    public async Task PostUpload_WithMatchingHash_Returns201WithStorageKey()
    {
        var captureId = await RegisterCaptureAsync();
        var response = await _client.PostAsJsonAsync("/upload", new UploadImageRequest(captureId, SampleImage));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        body!.StorageKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostUpload_WithMatchingHash_SetsLocationHeader()
    {
        var captureId = await RegisterCaptureAsync();
        var response = await _client.PostAsJsonAsync("/upload", new UploadImageRequest(captureId, SampleImage));

        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task PostUpload_UnknownCaptureId_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/upload",
            new UploadImageRequest(Guid.NewGuid(), SampleImage));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostUpload_WhenHashMismatches_Returns400()
    {
        var captureId = await RegisterCaptureAsync(hashHex: new string('b', 64));
        var response = await _client.PostAsJsonAsync("/upload",
            new UploadImageRequest(captureId, SampleImage));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostUpload_WhenAlreadyUploaded_Returns409()
    {
        var captureId = await RegisterCaptureAsync();
        var request = new UploadImageRequest(captureId, SampleImage);
        await _client.PostAsJsonAsync("/upload", request);

        var response = await _client.PostAsJsonAsync("/upload", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostUpload_WhenAlreadyUploaded_RecordsDuplicateUploadSecurityEvent()
    {
        var captureId = await RegisterCaptureAsync();
        var request = new UploadImageRequest(captureId, SampleImage);
        await _client.PostAsJsonAsync("/upload", request);
        await _client.PostAsJsonAsync("/upload", request); // duplicate

        var eventsRes = await _client.GetAsync("/security/events?limit=50");
        var body = await eventsRes.Content.ReadFromJsonAsync<SecurityEventListDto>();
        body!.Events.Should().Contain(e => e.EventType == "DuplicateUpload");
    }

    private sealed record SecurityEventListDto(List<SecurityEventDto> Events);
    private sealed record SecurityEventDto(string EventType, string Severity, string Message);
}
