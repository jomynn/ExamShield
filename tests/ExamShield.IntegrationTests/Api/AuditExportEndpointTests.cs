using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class AuditExportEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _captureId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        (_captureId, _) = await TestHelpers.RegisterCaptureAsync(_client, factory.ActiveExamId);
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetAuditExport_Returns200WithCsvContentType()
    {
        var response = await _client.GetAsync("/audit/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    [Fact]
    public async Task GetAuditExport_ContainsCsvHeader()
    {
        var response = await _client.GetAsync("/audit/export");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Contain("Id");
        content.Should().Contain("Action");
        content.Should().Contain("OccurredAt");
    }

    [Fact]
    public async Task GetAuditExport_FilteredByCaptureId_ContainsCaptureEntry()
    {
        var response = await _client.GetAsync($"/audit/export?captureId={_captureId}");
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Contain("CaptureRegistered");
        content.Should().Contain(_captureId.ToString());
    }

    [Fact]
    public async Task GetAuditExport_WithFutureFromDate_ReturnsHeaderOnly()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var response = await _client.GetAsync($"/audit/export?from={Uri.EscapeDataString(future)}");
        var content = await response.Content.ReadAsStringAsync();

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1, "no entries should match a future 'from' date");
    }

    [Fact]
    public async Task GetAuditExport_Unauthenticated_Returns401()
    {
        using var anon = factory.CreateClient();
        var response = await anon.GetAsync("/audit/export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditExport_FilenameSuggestsDownload()
    {
        var response = await _client.GetAsync("/audit/export");

        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.FileName.Should().Contain("audit");
    }
}
