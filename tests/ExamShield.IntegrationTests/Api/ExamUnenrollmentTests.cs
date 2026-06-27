using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamUnenrollmentTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;
    private readonly Guid _studentId = Guid.NewGuid();

    public ExamUnenrollmentTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Unenroll Test Exam", null, 2));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        await _client.PostAsJsonAsync($"/exams/{_examId}/students",
            new EnrollStudentRequest(_studentId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Unenroll_EnrolledStudentNoCapture_Returns204()
    {
        var res = await _client.DeleteAsync($"/exams/{_examId}/students/{_studentId}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Unenroll_NotEnrolledStudent_Returns404()
    {
        var res = await _client.DeleteAsync($"/exams/{_examId}/students/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Unenroll_AfterCapture_Returns409()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Unenroll Dev", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var img     = System.Text.Encoding.UTF8.GetBytes("unenroll-capture");
        var hashHex = Convert.ToHexString(SHA256.HashData(img)).ToLowerInvariant();
        await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(_examId, _studentId, device!.DeviceId, 1,
                hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));

        var res = await _client.DeleteAsync($"/exams/{_examId}/students/{_studentId}");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Unenroll_UnknownExam_Returns404()
    {
        var res = await _client.DeleteAsync($"/exams/{Guid.NewGuid()}/students/{_studentId}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Unenroll_StudentsListNoLongerContainsStudent()
    {
        await _client.DeleteAsync($"/exams/{_examId}/students/{_studentId}");

        var listRes = await _client.GetAsync($"/exams/{_examId}/students");
        var candidates = await listRes.Content.ReadFromJsonAsync<ExamCandidatesResponse>();
        Assert.DoesNotContain(candidates!.Candidates, s => s.StudentId == _studentId);
    }
}
