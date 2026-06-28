using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class ScoreEndpointsTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _captureId;
    private Guid _examId;

    private static readonly byte[] ImageBytes = "score-test-exam-image"u8.ToArray();
    private static readonly string HashHex =
        Convert.ToHexString(SHA256.HashData(ImageBytes)).ToLowerInvariant();

    public ScoreEndpointsTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var deviceResponse = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Score-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        // Create and activate a dedicated exam for this test class to avoid cross-test pollution
        // when one test publishes results and another asserts results are not yet published.
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Score Test Exam", null, 50));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        // Set answer key — StubOcrService returns Q1=A, Q2=B, Q3=C
        await _client.PostAsJsonAsync($"/exams/{_examId}/answer-key",
            new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" }));

        var enrolledStudentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(enrolledStudentId));

        var captureResponse = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: _examId, StudentId: enrolledStudentId,
            DeviceId: device!.DeviceId, PageNumber: 1,
            HashHex: HashHex,
            SignatureBytes: _ecdsa.SignHash(Convert.FromHexString(HashHex))));
        var capture = await captureResponse.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = capture!.CaptureId;

        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(_captureId, ImageBytes));
        await _client.PostAsync($"/ocr/{_captureId}", null);
    }

    public Task DisposeAsync()
    {
        _ecdsa.Dispose();
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PostScore_WithOcrResult_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostScore_WithOcrResult_ReturnsScoreId()
    {
        var response = await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));
        var body = await response.Content.ReadFromJsonAsync<ScoreCaptureResponse>();
        body!.ScoreId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PostScore_ReturnsCorrectAndTotalAnswers()
    {
        var response = await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));
        var body = await response.Content.ReadFromJsonAsync<ScoreCaptureResponse>();
        body!.TotalQuestions.Should().Be(3);
    }

    [Fact]
    public async Task PostPublish_AfterScoring_Returns200WithPublishedCount()
    {
        await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));

        var response = await _client.PostAsJsonAsync("/results/publish",
            new PublishResultsRequest(_examId));
        var body = await response.Content.ReadFromJsonAsync<PublishResultsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.PublishedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetResults_AfterScoringAndPublishing_IncludesScore()
    {
        await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));
        await _client.PostAsJsonAsync("/results/publish", new PublishResultsRequest(_examId));

        var response = await _client.GetAsync("/results");
        var body = await response.Content.ReadFromJsonAsync<GetResultsResponse>();

        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetResults_BeforePublishing_DoesNotIncludeThisExamsScore()
    {
        await _client.PostAsJsonAsync("/score", new ScoreCaptureRequest(_captureId));

        var response = await _client.GetAsync("/results");
        var body = await response.Content.ReadFromJsonAsync<GetResultsResponse>();

        // Score is created but not yet published — this exam's results must not appear
        body!.Results.Should().NotContain(r => r.ExamId == _examId);
    }

    [Fact]
    public async Task GetResults_Returns200()
    {
        var response = await _client.GetAsync("/results");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStatistics_Returns200()
    {
        var response = await _client.GetAsync("/statistics");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostScore_Unauthenticated_Returns401()
    {
        using var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/score",
            new ScoreCaptureRequest(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPublish_Unauthenticated_Returns401()
    {
        using var unauthClient = _factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/results/publish",
            new PublishResultsRequest(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
