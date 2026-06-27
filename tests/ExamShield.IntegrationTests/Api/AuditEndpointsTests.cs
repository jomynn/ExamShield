using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class AuditEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public AuditEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var response = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Audit-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
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

    private async Task<Guid> RegisterCaptureAsync()
    {
        var hashHex = new string('a', 64);
        var request = new RegisterCaptureRequest(
            _factory.ActiveExamId, Guid.NewGuid(), _deviceId, 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);
        var body = await response.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return body!.CaptureId;
    }

    [Fact]
    public async Task GetAudit_ReturnsOk()
    {
        var response = await _client.GetAsync("/audit");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAudit_AfterRegisterCapture_ContainsCaptureRegisteredEntry()
    {
        var captureId = await RegisterCaptureAsync();

        var response = await _client.GetAsync($"/audit?captureId={captureId}");
        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        body!.Entries.Should().Contain(e =>
            e.Action == "CaptureRegistered" && e.CaptureId == captureId);
    }

    [Fact]
    public async Task GetAudit_FilterByCaptureId_ReturnsOnlyMatchingEntries()
    {
        var captureId = await RegisterCaptureAsync();
        await RegisterCaptureAsync();

        var response = await _client.GetAsync($"/audit?captureId={captureId}");
        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        body!.Entries.Should().AllSatisfy(e => e.CaptureId.Should().Be(captureId));
    }

    [Fact]
    public async Task GetAudit_DefaultPagination_Returns50PerPage()
    {
        var response = await _client.GetAsync("/audit");
        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        body!.Entries.Count.Should().BeLessThanOrEqualTo(50);
    }
}
