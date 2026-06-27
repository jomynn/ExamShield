using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class CaptureStudentFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _studentAId;
    private Guid _studentBId;
    private Guid _captureAId;
    private Guid _captureBId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var dev = await (await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("StudentFilterDev", ecdsa.ExportSubjectPublicKeyInfo())))
            .Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        var deviceId = dev!.DeviceId;
        await _client.PutAsync($"/devices/{deviceId}/approve", null);

        var examId = factory.ActiveExamId;
        _studentAId = Guid.NewGuid();
        _studentBId = Guid.NewGuid();

        async Task<Guid> RegisterAsync(Guid studentId, int page)
        {
            var bytes = new byte[] { (byte)page, 0, 0, 1 };
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sig  = ecdsa.SignHash(hash);
            var res  = await _client.PostAsJsonAsync("/capture",
                new RegisterCaptureRequest(examId, studentId, deviceId, page,
                    Convert.ToHexString(hash), sig));
            var dto = await res.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
            return dto!.CaptureId;
        }

        _captureAId = await RegisterAsync(_studentAId, 1);
        _captureBId = await RegisterAsync(_studentBId, 2);
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetCaptures_WithStudentIdFilter_ReturnsOnlyThatStudentsCaptures()
    {
        var res  = await _client.GetAsync($"/captures?studentId={_studentAId}");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        var ids  = body!.Captures.Select(c => c.CaptureId).ToHashSet();

        Assert.Contains(_captureAId, ids);
        Assert.DoesNotContain(_captureBId, ids);
    }

    [Fact]
    public async Task GetCaptures_StudentIdFilter_ExcludesOtherStudents()
    {
        var res  = await _client.GetAsync($"/captures?studentId={_studentAId}");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();

        Assert.All(body!.Captures, c => Assert.Equal(_studentAId, c.StudentId));
    }

    [Fact]
    public async Task GetCaptures_NoStudentIdFilter_ReturnsBothStudents()
    {
        var res  = await _client.GetAsync("/captures");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();
        var ids  = body!.Captures.Select(c => c.CaptureId).ToHashSet();

        Assert.Contains(_captureAId, ids);
        Assert.Contains(_captureBId, ids);
    }

    [Fact]
    public async Task GetCaptures_UnknownStudentId_ReturnsEmpty()
    {
        var res  = await _client.GetAsync($"/captures?studentId={Guid.NewGuid()}");
        var body = await res.Content.ReadFromJsonAsync<CaptureListResponse>();

        Assert.NotNull(body);
        Assert.Empty(body.Captures);
    }
}
