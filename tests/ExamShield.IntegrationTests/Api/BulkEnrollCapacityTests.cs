using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class BulkEnrollCapacityTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private static string BulkRoute(Guid examId) => $"/exams/{examId}/students/bulk";

    [Fact]
    public async Task BulkEnroll_ExceedingMaxCandidates_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Bulk Cap Exam", null, 5, MaxCandidates: 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var res = await client.PostAsJsonAsync(BulkRoute(exam.ExamId),
            new BulkEnrollRequest([Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task BulkEnroll_WithinMaxCandidates_Succeeds()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Bulk Under Cap", null, 5, MaxCandidates: 5));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var res = await client.PostAsJsonAsync(BulkRoute(exam.ExamId),
            new BulkEnrollRequest([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BulkEnrollResponse>();
        Assert.Equal(3, body!.Enrolled);
    }
}
