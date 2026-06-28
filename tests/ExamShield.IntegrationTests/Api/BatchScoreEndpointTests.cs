using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class BatchScoreEndpointTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;

    public BatchScoreEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Batch Score Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        await _client.PostAsJsonAsync($"/exams/{_examId}/answer-key",
            new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" }));

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Batch Score Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(studentId));
        var imageBytes = System.Text.Encoding.UTF8.GetBytes("batch-score-test-image");
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        var capRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            _examId, studentId, device!.DeviceId, 1,
            hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
        var cap = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(cap!.CaptureId, imageBytes));
        await _client.PostAsync($"/ocr/{cap.CaptureId}", null);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BatchScore_WithOcrCompletedCaptures_Returns200WithCounts()
    {
        var response = await _client.PostAsJsonAsync("/score/batch", new { examId = _examId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BatchScoreResponse>();
        Assert.NotNull(body);
        Assert.True(body.Scored + body.Skipped >= 1);
    }

    [Fact]
    public async Task BatchScore_WithUnknownExam_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/score/batch", new { examId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BatchScore_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/score/batch", new { examId = _examId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
