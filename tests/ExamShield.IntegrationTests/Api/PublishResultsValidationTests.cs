using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class PublishResultsValidationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private sealed record PublishRequest(Guid ExamId);

    [Fact]
    public async Task PublishResults_WhenNoScoresExist_Returns422()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("No-Score Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var res = await client.PostAsJsonAsync("/results/publish",
            new PublishRequest(exam.ExamId));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task PublishResults_WhenAlreadyPublished_Returns409()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var deviceRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Publish-Dup Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await deviceRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Already-Published Exam", null, 1));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await client.PutAsync($"/exams/{exam!.ExamId}/activate", null);
        await client.PostAsJsonAsync("/exams/" + exam.ExamId + "/answer-key",
            new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A" }));

        var imageBytes = "already-published-test"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

        var studentId = Guid.NewGuid();
        await client.PostAsJsonAsync($"/exams/{exam.ExamId}/students", new EnrollStudentRequest(studentId));

        var captureRes = await client.PostAsJsonAsync("/capture", new RegisterCaptureRequest(
            ExamId: exam.ExamId, StudentId: studentId,
            DeviceId: device.DeviceId, PageNumber: 1,
            HashHex: hash,
            SignatureBytes: ecdsa.SignHash(Convert.FromHexString(hash))));
        var capture = await captureRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        await client.PostAsJsonAsync("/upload", new UploadImageRequest(capture!.CaptureId, imageBytes));
        await client.PostAsync($"/ocr/{capture.CaptureId}", null);
        await client.PostAsJsonAsync("/score", new { captureId = capture.CaptureId });

        await client.PostAsJsonAsync("/results/publish", new PublishRequest(exam.ExamId));

        var secondPublish = await client.PostAsJsonAsync("/results/publish",
            new PublishRequest(exam.ExamId));

        Assert.Equal(HttpStatusCode.Conflict, secondPublish.StatusCode);
    }
}
