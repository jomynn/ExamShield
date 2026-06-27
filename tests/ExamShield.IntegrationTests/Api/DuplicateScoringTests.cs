using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DuplicateScoringTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _captureId;

    private static readonly byte[] ImageBytes = "duplicate-score-test-image"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public DuplicateScoringTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Dup-Score Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Dup Score Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        var examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{examId}/activate", null);

        var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: examId, StudentId: Guid.NewGuid(),
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: _ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
        await _client.PostAsync($"/ocr/{_captureId}", null);
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task Score_FirstCall_Returns200()
    {
        var res = await _client.PostAsJsonAsync("/score", new { captureId = _captureId });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Score_SecondCall_Returns409()
    {
        await _client.PostAsJsonAsync("/score", new { captureId = _captureId });
        var res = await _client.PostAsJsonAsync("/score", new { captureId = _captureId });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
