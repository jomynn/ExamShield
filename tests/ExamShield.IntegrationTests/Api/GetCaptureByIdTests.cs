using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class GetCaptureByIdTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _examId;
    private Guid _deviceId;
    private ECDsa _ecdsa = null!;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Detail Test Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Detail Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId  = device!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync() { _client.Dispose(); _ecdsa.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> RegisterCaptureAsync()
    {
        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{_examId}/students", new EnrollStudentRequest(studentId));

        var img     = System.Text.Encoding.UTF8.GetBytes($"capture-detail-{Guid.NewGuid()}");
        var hashHex = Convert.ToHexString(SHA256.HashData(img)).ToLowerInvariant();
        var req = new RegisterCaptureRequest(
            _examId, studentId, _deviceId, 1,
            hashHex, _ecdsa.SignHash(Convert.FromHexString(hashHex)));
        var res = await _client.PostAsJsonAsync("/capture", req);
        return (await res.Content.ReadFromJsonAsync<RegisterCaptureResponse>())!.CaptureId;
    }

    [Fact]
    public async Task GetCaptureById_ExistingId_Returns200WithHashAndSignature()
    {
        var id  = await RegisterCaptureAsync();
        var res = await _client.GetAsync($"/captures/{id}");
        var dto = await res.Content.ReadFromJsonAsync<CaptureDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(id, dto!.CaptureId);
        Assert.NotEmpty(dto.Hash);
        Assert.NotEmpty(dto.Signature);
        Assert.Equal("Created", dto.Status);
    }

    [Fact]
    public async Task GetCaptureById_UnknownId_Returns404()
    {
        var res = await _client.GetAsync($"/captures/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetCaptureById_Unauthenticated_Returns401()
    {
        var id  = await RegisterCaptureAsync();
        var res = await factory.CreateClient().GetAsync($"/captures/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

}
