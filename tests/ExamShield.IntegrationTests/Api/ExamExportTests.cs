using System.Net;
using System.Net.Http.Json;
using ExamShield.IntegrationTests.Helpers;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamExportTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ExportExams_NoFilter_ReturnsCsv()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await ExamHelper.CreateExamAsync(client, "Export-Exam-A");
        await ExamHelper.CreateExamAsync(client, "Export-Exam-B");

        var response = await client.GetAsync("/exams/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, $"Expected at least 3 lines (header + 2 rows), got {lines.Length}");
        Assert.Contains("ExamId", lines[0]);
        Assert.Contains("Export-Exam-A", csv);
        Assert.Contains("Export-Exam-B", csv);
    }

    [Fact]
    public async Task ExportExams_WithSearchFilter_ReturnsFilteredCsv()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await ExamHelper.CreateExamAsync(client, "FilteredExport-Alpha");
        await ExamHelper.CreateExamAsync(client, "FilteredExport-Beta");

        var response = await client.GetAsync("/exams/export?search=Alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("FilteredExport-Alpha", csv);
        Assert.DoesNotContain("FilteredExport-Beta", csv);
    }

    [Fact]
    public async Task ExportExams_WithStatusFilter_ReturnsFilteredCsv()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var activatedId = await ExamHelper.CreateExamAsync(client, "StatusExport-Active");
        await ExamHelper.CreateExamAsync(client, "StatusExport-Draft");

        await client.PutAsync($"/exams/{activatedId}/activate", null);

        var response = await client.GetAsync("/exams/export?status=Active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("StatusExport-Active", csv);
        Assert.DoesNotContain("StatusExport-Draft", csv);
    }

    [Fact]
    public async Task ExportExams_ContentDispositionHeaderSet()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/exams/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.EndsWith(".csv", disposition!.FileName?.Trim('"'));
    }

    [Fact]
    public async Task ExportExams_Unauthenticated_Returns401()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/exams/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
