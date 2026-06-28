using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ScoreBreakdownTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _captureId;

    private static readonly byte[] ImageBytes = "score-breakdown-test"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Breakdown Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Breakdown Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        await _client.PostAsJsonAsync("/exams/" + exam.ExamId + "/answer-key",
            new SetAnswerKeyRequest(new Dictionary<int, string>
                { [1] = "A", [2] = "B", [3] = "C" }));

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam!.ExamId}/students", new EnrollStudentRequest(studentId));
        var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: studentId,
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
        await _client.PostAsync($"/ocr/{_captureId}", null);
        await _client.PostAsJsonAsync("/score", new { captureId = _captureId });
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private sealed record BreakdownResponse(Guid CaptureId, int CorrectAnswers, int TotalQuestions, double Percentage, List<QuestionItem> Questions);
    private sealed record QuestionItem(int QuestionNumber, string StudentAnswer, string ExpectedAnswer, bool IsCorrect, string AnswerSource);

    [Fact]
    public async Task GetBreakdown_AfterScoring_ReturnsPerQuestionDetail()
    {
        var res = await _client.GetAsync($"/score/{_captureId}/breakdown");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<BreakdownResponse>();
        Assert.NotNull(body);
        Assert.Equal(_captureId, body.CaptureId);
        Assert.Equal(3, body.TotalQuestions);
        Assert.Equal(3, body.Questions.Count);
        Assert.All(body.Questions, q => Assert.Equal("OCR", q.AnswerSource));
    }

    [Fact]
    public async Task GetBreakdown_UnknownCapture_Returns404()
    {
        var res = await _client.GetAsync($"/score/{Guid.NewGuid()}/breakdown");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
