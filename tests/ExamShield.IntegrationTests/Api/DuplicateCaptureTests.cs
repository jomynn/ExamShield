using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DuplicateCaptureTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;
    private Guid _deviceId;
    private ECDsa _ecdsa = null!;
    private readonly Guid _studentId = Guid.NewGuid();

    public DuplicateCaptureTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();

        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Dup Capture Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        _examId = exam!.ExamId;
        await _client.PutAsync($"/exams/{_examId}/activate", null);

        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Dup Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId = device!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public void Dispose() => _ecdsa.Dispose();
    public Task DisposeAsync() { Dispose(); return Task.CompletedTask; }

    private RegisterCaptureRequest MakeCaptureRequest(int page = 1)
    {
        var imageBytes = System.Text.Encoding.UTF8.GetBytes($"dup-test-{page}");
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        return new RegisterCaptureRequest(_examId, _studentId, _deviceId, page,
            hashHex, _ecdsa.SignHash(Convert.FromHexString(hashHex)));
    }

    [Fact]
    public async Task RegisterCapture_SameStudentSamePage_Returns409()
    {
        var r1 = await _client.PostAsJsonAsync("/capture", MakeCaptureRequest(page: 1));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await _client.PostAsJsonAsync("/capture", MakeCaptureRequest(page: 1));
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task RegisterCapture_SameStudentDifferentPage_Returns201()
    {
        await _client.PostAsJsonAsync("/capture", MakeCaptureRequest(page: 1));

        var r2 = await _client.PostAsJsonAsync("/capture", MakeCaptureRequest(page: 2));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    [Fact]
    public async Task RegisterCapture_DifferentStudentSamePage_Returns201()
    {
        await _client.PostAsJsonAsync("/capture", MakeCaptureRequest(page: 1));

        var imageBytes = System.Text.Encoding.UTF8.GetBytes("dup-other-student");
        var hashHex = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        var r2 = await _client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            _examId, Guid.NewGuid(), _deviceId, 1,
            hashHex, _ecdsa.SignHash(Convert.FromHexString(hashHex))));

        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }
}
