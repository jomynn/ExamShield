using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests;

public static class TestHelpers
{
    public static async Task<(Guid CaptureId, Guid StudentId)> RegisterCaptureAsync(HttpClient client, Guid examId)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var devRes = await client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("Helper Device", ecdsa.ExportSubjectPublicKeyInfo()));
        var device = await devRes.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await client.PutAsync($"/devices/{device!.DeviceId}/approve", null);

        var hashHex = new string('a', 64);
        var studentId = Guid.NewGuid();
        var capRes = await client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                examId, studentId, device!.DeviceId, 1, hashHex,
                ecdsa.SignHash(Convert.FromHexString(hashHex))));

        var capture = await capRes.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        return (capture!.CaptureId, studentId);
    }
}
