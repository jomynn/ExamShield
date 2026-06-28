using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Helpers;

public static class CaptureHelper
{
    public static async Task<(Guid CaptureId, string HashHex)> RegisterCaptureAsync(
        HttpClient client, Guid examId)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Capture Helper Device", ecdsa.ExportSubjectPublicKeyInfo()));
        devRes.EnsureSuccessStatusCode();
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var studentId = Guid.NewGuid();
        await client.PostAsJsonAsync($"/exams/{examId}/students", new EnrollStudentRequest(studentId));

        // Unique hash per call so FindByHashAsync returns the correct capture
        var hashHex = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();

        var capRes = await client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                examId, studentId, device!.DeviceId, 1, hashHex,
                ecdsa.SignHash(Convert.FromHexString(hashHex))));
        capRes.EnsureSuccessStatusCode();
        var capture = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return (capture!.CaptureId, hashHex);
    }
}
