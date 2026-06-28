using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class SecurityEventsByCaptureTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _captureId;

    private static readonly byte[] ImageBytes = "sec-events-by-capture-test"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("SecEvt Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("SecEvt Exam", null, 5));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam!.ExamId}/students", new EnrollStudentRequest(studentId));

        var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: studentId,
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        // First upload succeeds; second triggers a DuplicateUpload security event with this captureId
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private sealed record EventListResponse(List<EventItem> Events);
    private sealed record EventItem(Guid? CaptureId);

    [Fact]
    public async Task GetSecurityEvents_WithCaptureId_ReturnsOnlyEventsForThatCapture()
    {
        var res = await _client.GetAsync($"/security/events?captureId={_captureId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<EventListResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Events);
        Assert.All(body.Events, e => Assert.Equal(_captureId, e.CaptureId));
    }

    [Fact]
    public async Task GetSecurityEvents_WithUnknownCaptureId_ReturnsEmpty()
    {
        var res = await _client.GetAsync($"/security/events?captureId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<EventListResponse>();
        Assert.Empty(body!.Events);
    }

    [Fact]
    public async Task GetSecurityEvents_NoCaptureId_ReturnsEvents()
    {
        var res = await _client.GetAsync("/security/events");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<EventListResponse>();
        Assert.NotNull(body?.Events);
    }
}
