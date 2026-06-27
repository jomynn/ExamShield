using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureExportTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;

    public CaptureExportTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Export Test Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Export Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var imageBytes = System.Text.Encoding.UTF8.GetBytes("export-test-image");
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            _examId, Guid.NewGuid(), device!.DeviceId, 1,
            hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExportCaptures_ReturnsOkWithCsvContentType()
    {
        var response = await _client.GetAsync($"/captures/export?examId={_examId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportCaptures_CsvContainsHeaderAndDataRow()
    {
        var response = await _client.GetAsync($"/captures/export?examId={_examId}");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains("CaptureId", csv);
        Assert.Contains("ExamId", csv);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2); // at least header + 1 row
    }

    [Fact]
    public async Task ExportCaptures_WithStatusFilter_Returns400ForUnknownStatus()
    {
        var response = await _client.GetAsync($"/captures/export?status=Invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExportCaptures_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.GetAsync($"/captures/export?examId={_examId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
