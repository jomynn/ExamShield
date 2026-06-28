using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DuplicateOcrTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _captureId;

    private static readonly byte[] ImageBytes = "duplicate-ocr-test-image"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public DuplicateOcrTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Dup-OCR Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Dup OCR Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/students",
            new EnrollStudentRequest(studentId));

        var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: studentId,
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: _ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task TriggerOcr_FirstCall_Returns200()
    {
        var res = await _client.PostAsync($"/ocr/{_captureId}", null);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
    }

    [Fact]
    public async Task TriggerOcr_SecondCall_Returns409()
    {
        await _client.PostAsync($"/ocr/{_captureId}", null);
        var res = await _client.PostAsync($"/ocr/{_captureId}", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
