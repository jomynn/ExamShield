using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using ExamShield.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class AnswerSheetImageEndpointTests
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _adminClient = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public AnswerSheetImageEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _adminClient = await _factory.CreateAuthenticatedClientAsync();
        var res = await _adminClient.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("ImageTest Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        var body = await res.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        _deviceId = body!.DeviceId;
        await _adminClient.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _adminClient.Dispose(); return Task.CompletedTask; }

    private static readonly byte[] SampleImage = "jpeg-answer-sheet-bytes"u8.ToArray();
    private static readonly string SampleHash =
        Convert.ToHexString(SHA256.HashData(SampleImage)).ToLower();

    private async Task<Guid> RegisterAndUploadCaptureAsync(
        HttpClient captureClient, HttpClient uploadClient)
    {
        var studentId = _factory.EnrollStudentDirectly(_factory.ActiveExamId);
        var req = new RegisterCaptureRequest(
            _factory.ActiveExamId, studentId, _deviceId, 1,
            SampleHash, _ecdsa.SignHash(Convert.FromHexString(SampleHash)));
        var res = await captureClient.PostAsJsonAsync("/capture", req);
        var body = await res.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        var captureId = body!.CaptureId;
        await uploadClient.PostAsJsonAsync("/upload", new UploadImageRequest(captureId, SampleImage));
        return captureId;
    }

    private Task<Guid> RegisterAndUploadCaptureAsync() =>
        RegisterAndUploadCaptureAsync(_adminClient, _adminClient);

    private async Task<Guid> RegisterCaptureOnlyAsync()
    {
        var studentId = _factory.EnrollStudentDirectly(_factory.ActiveExamId);
        var req = new RegisterCaptureRequest(
            _factory.ActiveExamId, studentId, _deviceId, 1,
            SampleHash, _ecdsa.SignHash(Convert.FromHexString(SampleHash)));
        var res = await _adminClient.PostAsJsonAsync("/capture", req);
        var body = await res.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return body!.CaptureId;
    }

    // ── Allowed non-scoped roles (can access any capture) ─────────────────

    [Theory]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.ManualReviewer)]
    [InlineData(UserRole.ReviewSupervisor)]
    public async Task GetImage_NonScopedAllowedRole_Returns200(UserRole role)
    {
        var captureId = await RegisterAndUploadCaptureAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync(role);

        var res = await client.GetAsync($"/captures/{captureId}/image");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    // ── Scoped roles (Operator/Invigilator) must own the capture ──────────

    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Invigilator)]
    public async Task GetImage_ScopedRole_OwnCapture_Returns200(UserRole role)
    {
        using var roleClient = await _factory.CreateAuthenticatedClientAsync(role);
        // Role registers its own capture so InvigilatorId = role's userId.
        var captureId = await RegisterAndUploadCaptureAsync(roleClient, roleClient);

        var res = await roleClient.GetAsync($"/captures/{captureId}/image");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    // ── InvestigationOfficer requires an MFA-verified session ─────────────

    [Fact]
    public async Task GetImage_InvestigationOfficer_WithMfa_Returns200()
    {
        var captureId = await RegisterAndUploadCaptureAsync();
        using var client = _factory.CreateMfaAuthenticatedClientAsync(UserRole.InvestigationOfficer);

        var res = await client.GetAsync($"/captures/{captureId}/image");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    // ── Blocked roles — must return 403, not just hide the button ─────────

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Auditor)]
    [InlineData(UserRole.SecurityOfficer)]
    [InlineData(UserRole.ExamManager)]
    [InlineData(UserRole.DeviceManager)]
    [InlineData(UserRole.ResultPublisher)]
    [InlineData(UserRole.ScoringEngine)]
    public async Task GetImage_BlockedRole_Returns403(UserRole role)
    {
        var captureId = await RegisterAndUploadCaptureAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync(role);

        var res = await client.GetAsync($"/captures/{captureId}/image");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"role {role} must not be able to retrieve raw image bytes");
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetImage_Unauthenticated_Returns401()
    {
        var captureId = await RegisterAndUploadCaptureAsync();
        using var anonClient = _factory.CreateClient();

        var res = await anonClient.GetAsync($"/captures/{captureId}/image");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetImage_NonExistentCapture_Returns404()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(UserRole.Supervisor);
        var res = await client.GetAsync($"/captures/{Guid.NewGuid()}/image");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetImage_CaptureWithoutUpload_Returns404()
    {
        var captureId = await RegisterCaptureOnlyAsync();
        // ManualReviewer has no scope restriction — bypasses InvigilatorId check.
        using var client = await _factory.CreateAuthenticatedClientAsync(UserRole.ManualReviewer);
        var res = await client.GetAsync($"/captures/{captureId}/image");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
