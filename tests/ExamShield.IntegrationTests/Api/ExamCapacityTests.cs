using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamCapacityTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private static string EnrollRoute(Guid examId) => $"/exams/{examId}/students";

    [Fact]
    public async Task Enroll_BeyondMaxCandidates_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Cap Test Exam", null, 5, MaxCandidates: 1));
        examRes.EnsureSuccessStatusCode();
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var first = await client.PostAsJsonAsync(EnrollRoute(exam.ExamId),
            new EnrollStudentRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync(EnrollRoute(exam.ExamId),
            new EnrollStudentRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enroll_WithNoMaxCandidates_AllowsManyStudents()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("No Cap Exam", null, 5));
        examRes.EnsureSuccessStatusCode();
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        for (var i = 0; i < 5; i++)
        {
            var res = await client.PostAsJsonAsync(EnrollRoute(exam.ExamId),
                new EnrollStudentRequest(Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }
    }

    [Fact]
    public async Task CreateExam_WithMaxCandidates_IsReturnedInResponse()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var res = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("MaxCap Exam", null, 10, MaxCandidates: 30));
        res.EnsureSuccessStatusCode();
        var exam = await res.Content.ReadFromJsonAsync<ExamResponse>();

        Assert.Equal(30, exam!.MaxCandidates);
    }
}
