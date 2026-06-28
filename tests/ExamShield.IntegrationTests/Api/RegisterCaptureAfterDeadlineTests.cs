using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class RegisterCaptureAfterDeadlineTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private sealed record CreateExamWithDates(
        string Name, string? Description, int TotalQuestions,
        DateTimeOffset? ScheduledAt, DateTimeOffset? EndsAt);

    [Fact]
    public async Task RegisterCapture_AfterEndsAt_Returns422()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var deviceRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Expired Test Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await client.PostAsJsonAsync("/exams/", new CreateExamWithDates(
            "Expired Exam", null, 1,
            ScheduledAt: DateTimeOffset.UtcNow.AddHours(-3),
            EndsAt: DateTimeOffset.UtcNow.AddSeconds(-1)));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var image = "expired-exam-test"u8.ToArray();
        var hash  = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        var sig   = ecdsa.SignHash(Convert.FromHexString(hash));

        var captureRes = await client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: Guid.NewGuid(),
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: hash, SignatureBytes: sig));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, captureRes.StatusCode);
    }

    [Fact]
    public async Task RegisterCapture_WithNoEndsAt_Succeeds()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var deviceRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("No-Deadline Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("No Deadline Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var image = "no-deadline-exam"u8.ToArray();
        var hash  = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();
        var sig   = ecdsa.SignHash(Convert.FromHexString(hash));

        var studentId = Guid.NewGuid();
        await client.PostAsJsonAsync($"/exams/{exam!.ExamId}/students", new EnrollStudentRequest(studentId));

        var captureRes = await client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: studentId,
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: hash, SignatureBytes: sig));

        Assert.Equal(HttpStatusCode.Created, captureRes.StatusCode);
    }
}
