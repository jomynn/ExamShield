using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamStatisticsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetExamStatistics_NoScores_ReturnsZeroStats()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var examId = Guid.NewGuid();

        var res  = await client.GetAsync($"/score/exams/{examId}/statistics");
        var body = await res.Content.ReadFromJsonAsync<ExamStatisticsResponse>();

        res.EnsureSuccessStatusCode();
        Assert.NotNull(body);
        Assert.Equal(0, body.TotalStudents);
        Assert.Equal(0, body.PassRate);
        Assert.Equal(0, body.AveragePercentage);
        Assert.NotNull(body.GradeDistribution);
    }

    [Fact]
    public async Task GetExamStatistics_WithScores_ReturnsCorrectPassRate()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var examId = await factory.CreateActivatedExamAsync("Stats-Exam", totalQuestions: 10);

        await factory.RegisterScoreForStudentAsync(examId, correctAnswers: 9);  // 90% → pass
        await factory.RegisterScoreForStudentAsync(examId, correctAnswers: 8);  // 80% → pass
        await factory.RegisterScoreForStudentAsync(examId, correctAnswers: 5);  // 50% → fail

        var res  = await client.GetAsync($"/score/exams/{examId}/statistics");
        var body = await res.Content.ReadFromJsonAsync<ExamStatisticsResponse>();

        res.EnsureSuccessStatusCode();
        Assert.Equal(3, body!.TotalStudents);
        Assert.Equal(66.67, body.PassRate);
        Assert.Equal(1, body.GradeDistribution["A"]);
        Assert.Equal(1, body.GradeDistribution["B"]);
        Assert.Equal(1, body.GradeDistribution["F"]);
    }

    [Fact]
    public async Task GetExamStatistics_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync($"/score/exams/{Guid.NewGuid()}/statistics");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
