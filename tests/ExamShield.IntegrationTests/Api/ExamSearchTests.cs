using System.Net;
using System.Net.Http.Json;
using ExamShield.Application.Queries.GetExams;
using ExamShield.IntegrationTests.Helpers;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamSearchTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetExams_NoFilter_ReturnsAll()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await ExamHelper.CreateExamAsync(client, "Mathematics Final");
        await ExamHelper.CreateExamAsync(client, "Physics Final");

        var response = await client.GetAsync("/exams");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExamListResult>();

        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 2);
    }

    [Fact]
    public async Task GetExams_SearchByName_ReturnsMatchingOnly()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await ExamHelper.CreateExamAsync(client, "Chemistry2026");
        await ExamHelper.CreateExamAsync(client, "Biology2026");

        var response = await client.GetAsync("/exams?search=Chemistry2026");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExamListResult>();

        Assert.NotNull(body);
        Assert.All(body!.Exams, e => Assert.Contains("Chemistry2026", e.Name));
        Assert.DoesNotContain(body.Exams, e => e.Name.Contains("Biology2026"));
    }

    [Fact]
    public async Task GetExams_SearchNoMatch_ReturnsEmpty()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/exams?search=zzz_no_match_xyz_999");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExamListResult>();

        Assert.NotNull(body);
        Assert.Empty(body!.Exams);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task GetExams_FilterByStatus_ReturnsMatchingOnly()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var examId = await ExamHelper.CreateExamAsync(client, "ActivatableExam72");
        await client.PutAsync($"/exams/{examId}/activate", null);

        var response = await client.GetAsync("/exams?status=Active");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExamListResult>();

        Assert.NotNull(body);
        Assert.All(body!.Exams, e => Assert.Equal("Active", e.Status));
    }

    [Fact]
    public async Task GetExams_SearchAndStatusCombined_FiltersCorrectly()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var examId = await ExamHelper.CreateExamAsync(client, "UniqueCombo72Exam");
        await client.PutAsync($"/exams/{examId}/activate", null);
        await ExamHelper.CreateExamAsync(client, "UniqueCombo72Exam_Draft");

        var response = await client.GetAsync("/exams?search=UniqueCombo72Exam&status=Active");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ExamListResult>();

        Assert.NotNull(body);
        Assert.All(body!.Exams, e => Assert.Equal("Active", e.Status));
        Assert.All(body.Exams, e => Assert.Contains("UniqueCombo72Exam", e.Name));
        Assert.DoesNotContain(body.Exams, e => e.Name.Contains("Draft"));
    }

    private sealed record ExamItem(Guid ExamId, string Name, string Status);
    private sealed record ExamListResult(List<ExamItem> Exams, int TotalCount, int Page, int PageSize, int TotalPages);
}
