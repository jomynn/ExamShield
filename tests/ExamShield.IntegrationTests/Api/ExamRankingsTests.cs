using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamRankingsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;

    public ExamRankingsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Rankings Test Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        await _client.PostAsJsonAsync($"/exams/{_examId}/answer-key",
            new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" }));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task RegisterScoreForStudentAsync(Guid studentId)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Rankings Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        // Hash must match the uploaded image bytes exactly
        var imageBytes = SHA256.HashData(Guid.NewGuid().ToByteArray()); // unique per student
        var hashHex    = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

        await _client.PostAsJsonAsync($"/exams/{_examId}/students",
            new EnrollStudentRequest(studentId));

        var capRes = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(_examId, studentId, device!.DeviceId, 1, hashHex,
                ecdsa.SignHash(Convert.FromHexString(hashHex))));
        capRes.EnsureSuccessStatusCode();
        var capture = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();

        (await _client.PostAsJsonAsync("/upload", new UploadImageRequest(capture!.CaptureId, imageBytes))).EnsureSuccessStatusCode();

        (await _client.PostAsync($"/ocr/{capture.CaptureId}", null)).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(capture.CaptureId))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRankings_EmptyExam_ReturnsEmptyList()
    {
        var res  = await _client.GetAsync($"/score/rankings/{_examId}");
        var body = await res.Content.ReadFromJsonAsync<ExamRankingsResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Empty(body!.Rankings);
        Assert.Equal(_examId, body.ExamId);
    }

    [Fact]
    public async Task GetRankings_WithScores_ReturnsOrderedList()
    {
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        await RegisterScoreForStudentAsync(studentA);
        await RegisterScoreForStudentAsync(studentB);

        var res  = await _client.GetAsync($"/score/rankings/{_examId}");
        var body = await res.Content.ReadFromJsonAsync<ExamRankingsResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(2, body!.Rankings.Count);
        // All students scored identically (stub OCR), so both should share rank 1
        Assert.All(body.Rankings, r => Assert.Equal(1, r.Rank));
    }

    [Fact]
    public async Task GetRankings_ResponseIncludesCorrectFields()
    {
        var studentId = Guid.NewGuid();
        await RegisterScoreForStudentAsync(studentId);

        var res  = await _client.GetAsync($"/score/rankings/{_examId}");
        var body = await res.Content.ReadFromJsonAsync<ExamRankingsResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var entry = body!.Rankings.First();
        Assert.True(entry.Rank >= 1);
        Assert.True(entry.TotalQuestions > 0);
        Assert.InRange(entry.Percentage, 0.0, 100.0);
    }

    [Fact]
    public async Task GetRankings_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var res    = await client.GetAsync($"/score/rankings/{_examId}");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
