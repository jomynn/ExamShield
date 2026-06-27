using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class GetResultsByExamTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _examA;
    private Guid _examB;

    private static readonly byte[] ImageA = "results-exam-a"u8.ToArray();
    private static readonly byte[] ImageB = "results-exam-b"u8.ToArray();
    private static readonly string HashA = Convert.ToHexString(SHA256.HashData(ImageA)).ToLowerInvariant();
    private static readonly string HashB = Convert.ToHexString(SHA256.HashData(ImageB)).ToLowerInvariant();

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Results Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        async Task<Guid> SetupExam(string name, byte[] image, string hash)
        {
            var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest(name, null, 1));
            var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
            await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);
            await _client.PostAsJsonAsync("/exams/" + exam.ExamId + "/answer-key",
                new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A" }));

            var captureRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
                ExamId: exam.ExamId, StudentId: Guid.NewGuid(),
                DeviceId: device.DeviceId, PageNumber: 1,
                HashHex: hash,
                SignatureBytes: ecdsa.SignHash(Convert.FromHexString(hash))));
            var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
            await _client.PostAsJsonAsync("/upload", new UploadImageRequest(capture!.CaptureId, image));
            await _client.PostAsync($"/ocr/{capture.CaptureId}", null);
            await _client.PostAsJsonAsync("/score", new { captureId = capture.CaptureId });
            await _client.PostAsJsonAsync("/results/publish", new { examId = exam.ExamId });
            return exam.ExamId;
        }

        _examA = await SetupExam("Results Exam A", ImageA, HashA);
        _examB = await SetupExam("Results Exam B", ImageB, HashB);
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private sealed record ResultsResponse(List<ResultItem> Results);
    private sealed record ResultItem(Guid ExamId);

    [Fact]
    public async Task GetResults_WithExamId_ReturnsOnlyThatExam()
    {
        var res = await _client.GetAsync($"/results?examId={_examA}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ResultsResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Results);
        Assert.All(body.Results, r => Assert.Equal(_examA, r.ExamId));
    }

    [Fact]
    public async Task GetResults_WithoutExamId_ReturnsBothExams()
    {
        var res = await _client.GetAsync("/results");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ResultsResponse>();
        Assert.NotNull(body);
        var examIds = body.Results.Select(r => r.ExamId).Distinct().ToHashSet();
        Assert.Contains(_examA, examIds);
        Assert.Contains(_examB, examIds);
    }
}
