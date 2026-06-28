using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.GetChainOfCustody;

namespace ExamShield.IntegrationTests.Api;

public sealed class FlagCaptureAsTamperedTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _captureId;

    public FlagCaptureAsTamperedTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Flag Tamper Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Flag Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/students", new EnrollStudentRequest(studentId));

        var img     = System.Text.Encoding.UTF8.GetBytes("flag-tamper-test");
        var hashHex = Convert.ToHexString(SHA256.HashData(img)).ToLowerInvariant();
        var capRes  = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(exam.ExamId, studentId, device!.DeviceId, 1,
                hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
        var cap = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = cap!.CaptureId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FlagAsTampered_ValidCapture_Returns204()
    {
        var res = await _client.PostAsJsonAsync(
            $"/captures/{_captureId}/flag-tampered",
            new FlagTamperedRequest("ink alteration detected"));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task FlagAsTampered_ChainOfCustodyShowsTampered()
    {
        await _client.PostAsJsonAsync(
            $"/captures/{_captureId}/flag-tampered",
            new FlagTamperedRequest("erased pencil marks"));

        var chain = await _client.GetFromJsonAsync<GetChainOfCustodyResult>(
            $"/captures/{_captureId}/chain-of-custody");

        Assert.Equal("Tampered", chain!.Status);
        Assert.Contains(chain.AuditTrail, a => a.Action == "TamperingDetected");
    }

    [Fact]
    public async Task FlagAsTampered_AlreadyTampered_Returns409()
    {
        await _client.PostAsJsonAsync(
            $"/captures/{_captureId}/flag-tampered",
            new FlagTamperedRequest("first flag"));

        var res = await _client.PostAsJsonAsync(
            $"/captures/{_captureId}/flag-tampered",
            new FlagTamperedRequest("second flag"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task FlagAsTampered_UnknownCapture_Returns404()
    {
        var res = await _client.PostAsJsonAsync(
            $"/captures/{Guid.NewGuid()}/flag-tampered",
            new FlagTamperedRequest("reason"));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task FlagAsTampered_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.PostAsJsonAsync(
            $"/captures/{_captureId}/flag-tampered",
            new FlagTamperedRequest("reason"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
