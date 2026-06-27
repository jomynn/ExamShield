using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamReportEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetExamReport_WithActiveExam_Returns200()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/reports/exam/{factory.ActiveExamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExamReport_WithActiveExam_ReturnsExamName()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/reports/exam/{factory.ActiveExamId}");
        var body = await response.Content.ReadFromJsonAsync<ExamReportResponse>();

        body!.ExamName.Should().Be("Integration Test Exam");
    }

    [Fact]
    public async Task GetExamReport_WithUnknownExam_Returns404()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/reports/exam/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExamReport_Unauthenticated_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/reports/exam/{factory.ActiveExamId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExamReport_AfterCaptures_ReturnsCorrectCount()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        // Register two captures for the active exam
        await TestHelpers.RegisterCaptureAsync(client, factory.ActiveExamId);
        await TestHelpers.RegisterCaptureAsync(client, factory.ActiveExamId);

        var response = await client.GetAsync($"/reports/exam/{factory.ActiveExamId}");
        var body = await response.Content.ReadFromJsonAsync<ExamReportResponse>();

        body!.TotalCaptures.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetExamReportCsv_WithActiveExam_Returns200WithCsvContentType()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/reports/exam/{factory.ActiveExamId}/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }
}
