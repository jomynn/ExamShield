using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamSubmissionStatusTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;
    private Guid _enrolledStudentId;
    private Guid _submittedStudentId;

    public ExamSubmissionStatusTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Status Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        // Enroll two students
        _enrolledStudentId  = Guid.NewGuid();
        _submittedStudentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(_enrolledStudentId));
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(_submittedStudentId));

        // Register + upload a capture for _submittedStudentId only
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Status Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var imageBytes = System.Text.Encoding.UTF8.GetBytes("status-test-image");
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            _examId, _submittedStudentId, device!.DeviceId, 1,
            hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetSubmissionStatus_Returns200()
    {
        var response = await _client.GetAsync($"/exams/{_examId}/submission-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubmissionStatus_ReturnsCorrectCounts()
    {
        var response = await _client.GetAsync($"/exams/{_examId}/submission-status");
        var body = await response.Content.ReadFromJsonAsync<ExamSubmissionStatusResponse>();

        Assert.Equal(2, body!.TotalEnrolled);
        Assert.Equal(1, body.Submitted);
        Assert.Equal(1, body.Missing);
    }

    [Fact]
    public async Task GetSubmissionStatus_IdentifiesMissingStudent()
    {
        var response = await _client.GetAsync($"/exams/{_examId}/submission-status");
        var body = await response.Content.ReadFromJsonAsync<ExamSubmissionStatusResponse>();

        Assert.Contains(body!.Students, s => s.StudentId == _enrolledStudentId && !s.HasSubmitted);
        Assert.Contains(body.Students, s => s.StudentId == _submittedStudentId && s.HasSubmitted);
    }

    [Fact]
    public async Task GetSubmissionStatus_NoEnrollments_ReturnsZeroCounts()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Empty Status Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();

        var response = await _client.GetAsync($"/exams/{exam!.ExamId}/submission-status");
        var body = await response.Content.ReadFromJsonAsync<ExamSubmissionStatusResponse>();

        Assert.Equal(0, body!.TotalEnrolled);
        Assert.Equal(0, body.Missing);
    }

    [Fact]
    public async Task GetSubmissionStatus_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.GetAsync($"/exams/{_examId}/submission-status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
