using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamStateEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private Guid _deviceId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        var devRes = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("ExamState-Test Device", _ecdsa.ExportSubjectPublicKeyInfo()));
        _deviceId = (await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>())!.DeviceId;
        await _client.PutAsync($"/devices/{_deviceId}/approve", null);
    }

    public Task DisposeAsync() { _ecdsa.Dispose(); _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task ActivateExam_WhenDraft_Returns204()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Test Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();

        var response = await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ActivateExam_WhenAlreadyActive_Returns422()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Active Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var response = await _client.PutAsync($"/exams/{exam.ExamId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CloseExam_WhenActive_Returns204()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Close Test Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var response = await _client.PutAsync($"/exams/{exam.ExamId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostCapture_WithDraftExam_Returns422()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Draft Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();

        var hashHex = new string('a', 64);
        var request = new RegisterCaptureRequest(
            exam!.ExamId, Guid.NewGuid(), _deviceId, 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostCapture_WithClosedExam_Returns422()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Closed Exam", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);
        await _client.PutAsync($"/exams/{exam.ExamId}/close", null);

        var hashHex = new string('a', 64);
        var request = new RegisterCaptureRequest(
            exam.ExamId, Guid.NewGuid(), _deviceId, 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostCapture_WithActiveExam_Returns201()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Active Exam For Capture", null, 10));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var studentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/students", new EnrollStudentRequest(studentId));

        var hashHex = new string('a', 64);
        var request = new RegisterCaptureRequest(
            exam.ExamId, studentId, _deviceId, 1, hashHex,
            _ecdsa.SignHash(Convert.FromHexString(hashHex)));

        var response = await _client.PostAsJsonAsync("/capture", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
