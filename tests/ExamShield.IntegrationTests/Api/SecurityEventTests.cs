using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class SecurityEventTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetSecurityEvents_ReturnsOk()
    {
        var response = await _client.GetAsync("/security/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSecurityEvents_ReturnsEventList()
    {
        var response = await _client.GetAsync("/security/events");
        var body = await response.Content.ReadFromJsonAsync<SecurityEventListResponse>();
        body.Should().NotBeNull();
        body!.Events.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSecurityEvents_AfterHashMismatch_ContainsHashMismatchEvent()
    {
        // 1. Register device
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubKey = ecdsa.ExportSubjectPublicKeyInfo();
        var devResp = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("SecTestDev", pubKey));
        var dev = await devResp.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{dev!.DeviceId}/approve", null);

        // 2. Register capture with real hash
        var realBytes = new byte[] { 1, 2, 3, 4, 5 };
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(realBytes);
        var sig = ecdsa.SignHash(hash);

        var capResp = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, Guid.NewGuid(), dev!.DeviceId, 1,
                Convert.ToHexString(hash), sig));
        var cap = await capResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();

        // 3. Upload wrong bytes → hash mismatch → SecurityEvent
        var wrongBytes = new byte[] { 9, 9, 9 };
        await _client.PostAsJsonAsync("/upload",
            new UploadImageRequest(cap!.CaptureId, wrongBytes));

        // 4. The security event should exist
        var evtResp = await _client.GetAsync("/security/events");
        var events = await evtResp.Content.ReadFromJsonAsync<SecurityEventListResponse>();
        events!.Events.Should().Contain(e => e.EventType == "HashMismatch");
    }

    [Fact]
    public async Task GetSecurityEvents_Unauthenticated_Returns401()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/security/events");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
