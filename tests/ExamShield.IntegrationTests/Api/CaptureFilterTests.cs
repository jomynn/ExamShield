using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureFilterTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examAId;
    private Guid _examBId;
    private Guid _captureAId;

    public CaptureFilterTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Filter Test Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        // Exam A — will have one capture
        var examARes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Filter Exam A", null, 5));
        var examA = await examARes.Content.ReadFromJsonAsync<ExamResponse>();
        _examAId = examA!.ExamId;
        await _client.PutAsync($"/exams/{_examAId}/activate", null);

        // Exam B — will have no captures
        var examBRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Filter Exam B", null, 5));
        var examB = await examBRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examBId = examB!.ExamId;
        await _client.PutAsync($"/exams/{_examBId}/activate", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{_examAId}/students", new EnrollStudentRequest(studentId));
        var hashHex = new string('a', 64);
        var capRes = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            _examAId, studentId, device!.DeviceId, 1,
            hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
        var cap = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureAId = cap!.CaptureId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCaptures_WithExamIdFilter_ReturnsOnlyCapturesForThatExam()
    {
        var response = await _client.GetAsync($"/captures?examId={_examAId}");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(body!.Captures, c => Assert.Equal(_examAId, c.ExamId));
        Assert.DoesNotContain(body.Captures, c => c.ExamId == _examBId);
    }

    [Fact]
    public async Task GetCaptures_WithExamIdFilter_ExcludesOtherExams()
    {
        var response = await _client.GetAsync($"/captures?examId={_examBId}");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(body!.Captures, c => c.ExamId == _examAId);
    }

    [Fact]
    public async Task GetCaptures_WithStatusFilter_ReturnsOnlyMatchingCaptures()
    {
        var response = await _client.GetAsync("/captures?status=Created");
        var body = await response.Content.ReadFromJsonAsync<CaptureListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(body!.Captures, c => Assert.Equal("Created", c.Status));
    }

    [Fact]
    public async Task GetCaptures_WithUnknownStatus_Returns400()
    {
        var response = await _client.GetAsync("/captures?status=Nonexistent");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
