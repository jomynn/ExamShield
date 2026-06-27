using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class AuditLogDateRangeTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private DateTimeOffset _before;
    private DateTimeOffset _after;

    private static readonly byte[] ImageBytes = "audit-daterange-test"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        _before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("AuditDate Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("AuditDate Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: Guid.NewGuid(),
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(capture!.CaptureId, ImageBytes));

        _after = DateTimeOffset.UtcNow.AddSeconds(1);
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private sealed record AuditResponse(List<object> Entries, int TotalCount);

    [Fact]
    public async Task GetAuditLog_WithFromBeforeEvents_ReturnsEntries()
    {
        var from = _before.ToString("O");
        var res = await _client.GetAsync($"/audit?from={Uri.EscapeDataString(from)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuditResponse>();
        Assert.NotNull(body);
        Assert.True(body.TotalCount > 0);
    }

    [Fact]
    public async Task GetAuditLog_WithToBeforeEvents_ReturnsEmpty()
    {
        var to = _before.AddMinutes(-5).ToString("O");
        var res = await _client.GetAsync($"/audit?to={Uri.EscapeDataString(to)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuditResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task GetAuditLog_WithFromAfterAllEvents_ReturnsEmpty()
    {
        var from = _after.AddMinutes(5).ToString("O");
        var res = await _client.GetAsync($"/audit?from={Uri.EscapeDataString(from)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuditResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body.TotalCount);
    }
}
