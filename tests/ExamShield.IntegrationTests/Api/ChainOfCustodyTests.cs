using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using ExamShield.Application.Queries.GetChainOfCustody;

namespace ExamShield.IntegrationTests.Api;

public sealed class ChainOfCustodyTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _captureId;

    public ChainOfCustodyTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Chain Test Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Chain Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/students", new EnrollStudentRequest(studentId));

        var img     = System.Text.Encoding.UTF8.GetBytes("chain-of-custody-test");
        var hashHex = Convert.ToHexString(SHA256.HashData(img)).ToLowerInvariant();
        var capRes  = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(exam.ExamId, studentId, device!.DeviceId, 1,
                hashHex, ecdsa.SignHash(Convert.FromHexString(hashHex))));
        var cap = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        _captureId = cap!.CaptureId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetChainOfCustody_ExistingCapture_Returns200()
    {
        var res = await _client.GetAsync($"/captures/{_captureId}/chain-of-custody");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetChainOfCustody_ReturnsCaptureMetadata()
    {
        var res = await _client.GetAsync($"/captures/{_captureId}/chain-of-custody");
        var chain = await res.Content.ReadFromJsonAsync<GetChainOfCustodyResult>();

        Assert.Equal(_captureId, chain!.CaptureId);
        Assert.Equal("Created", chain.Status);
        Assert.Equal(1, chain.PageNumber);
    }

    [Fact]
    public async Task GetChainOfCustody_AuditTrailContainsCaptureRegistered()
    {
        var res = await _client.GetAsync($"/captures/{_captureId}/chain-of-custody");
        var chain = await res.Content.ReadFromJsonAsync<GetChainOfCustodyResult>();

        Assert.Contains(chain!.AuditTrail, a => a.Action == "CaptureRegistered");
    }

    [Fact]
    public async Task GetChainOfCustody_UnknownCapture_Returns404()
    {
        var res = await _client.GetAsync($"/captures/{Guid.NewGuid()}/chain-of-custody");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetChainOfCustody_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var res = await anon.GetAsync($"/captures/{_captureId}/chain-of-custody");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
