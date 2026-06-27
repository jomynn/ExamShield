using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class AuditSignatureTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private HttpClient _client = null!;
    private Guid _captureId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        var deviceResp = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("SigTest Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceResp.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var hashHex = new string('b', 64);
        var captureResp = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, Guid.NewGuid(), device!.DeviceId,
                1, hashHex, _ecdsa.SignHash(Convert.FromHexString(hashHex))));
        var capture = await captureResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;
    }

    public Task DisposeAsync()
    {
        _ecdsa.Dispose();
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAudit_ByCaptureId_EntriesAllHaveServerSignature()
    {
        var response = await _client.GetAsync($"/audit?captureId={_captureId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();
        body!.Entries.Should().NotBeEmpty();
        body.Entries.Should().AllSatisfy(e =>
            e.ServerSignature.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetAudit_ServerSignatures_AreValidBase64()
    {
        var response = await _client.GetAsync($"/audit?captureId={_captureId}");
        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        foreach (var entry in body!.Entries)
        {
            var bytes = Convert.FromBase64String(entry.ServerSignature);
            bytes.Should().NotBeEmpty();
        }
    }
}
