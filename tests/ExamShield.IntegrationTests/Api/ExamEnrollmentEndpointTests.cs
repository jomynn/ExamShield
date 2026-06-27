using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamEnrollmentEndpointTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;
    private readonly Guid _studentId = Guid.NewGuid();

    public ExamEnrollmentEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Enrollment Exam", null, 5));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnrollStudent_Returns204()
    {
        var response = await _client.PostAsJsonAsync(
            $"/exams/{_examId}/students", new EnrollStudentRequest(_studentId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task EnrollStudent_ThenGetCandidates_ReturnsEnrolledStudent()
    {
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(_studentId));

        var response = await _client.GetAsync($"/exams/{_examId}/students");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ExamCandidatesResponse>();
        Assert.Contains(body!.Candidates, c => c.StudentId == _studentId);
    }

    [Fact]
    public async Task EnrollStudent_DuplicateEnrollment_Returns409()
    {
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(_studentId));
        var response = await _client.PostAsJsonAsync(
            $"/exams/{_examId}/students", new EnrollStudentRequest(_studentId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EnrollStudent_UnknownExam_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/exams/{Guid.NewGuid()}/students", new EnrollStudentRequest(_studentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCandidates_EmptyExam_ReturnsEmptyList()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Empty Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();

        var response = await _client.GetAsync($"/exams/{exam!.ExamId}/students");
        var body = await response.Content.ReadFromJsonAsync<ExamCandidatesResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body!.Candidates);
    }

    [Fact]
    public async Task EnrollStudent_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync(
            $"/exams/{_examId}/students", new EnrollStudentRequest(_studentId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
