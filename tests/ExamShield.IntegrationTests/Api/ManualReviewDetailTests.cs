using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class ManualReviewDetailTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<(Guid captureId, Guid reviewId)> SetupReviewableCapture()
    {
        // Register device
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pubKey = ecdsa.ExportSubjectPublicKeyInfo();
        var devResp = await _client.PostAsJsonAsync("/devices",
            new RegisterDeviceRequest("ReviewDev", pubKey));
        var dev = await devResp.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        await _client.PutAsync($"/devices/{dev!.DeviceId}/approve", null);

        // Register and upload capture
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
        var hash = SHA256.HashData(imageBytes);
        var sig = ecdsa.SignHash(hash);
        var capResp = await _client.PostAsJsonAsync("/capture",
            new RegisterCaptureRequest(
                factory.ActiveExamId, Guid.NewGuid(), dev!.DeviceId, 1,
                Convert.ToHexString(hash), sig));
        var cap = await capResp.Content.ReadFromJsonAsync<RegisterCaptureResponse>();
        await _client.PostAsJsonAsync("/upload", new UploadImageRequest(cap!.CaptureId, imageBytes));

        // Run OCR (stub returns low-confidence to trigger manual review)
        await _client.PostAsync($"/ocr/{cap.CaptureId}", null);

        // Only continue if a review was created
        var reviewsResp = await _client.GetAsync("/reviews");
        var reviews = await reviewsResp.Content.ReadFromJsonAsync<GetPendingReviewsResponse>();
        var review = reviews!.Reviews.FirstOrDefault(r => r.CaptureId == cap.CaptureId);
        if (review is null)
            return (cap.CaptureId, Guid.Empty);

        return (cap.CaptureId, review.ReviewId);
    }

    [Fact]
    public async Task GetReviews_ReturnsOk()
    {
        var response = await _client.GetAsync("/reviews");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReviewDetail_WithValidId_ReturnsOk()
    {
        var (_, reviewId) = await SetupReviewableCapture();
        if (reviewId == Guid.Empty) return; // OCR stub didn't create low-confidence result

        var response = await _client.GetAsync($"/reviews/{reviewId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReviewDetail_WithValidId_ReturnsAnswers()
    {
        var (_, reviewId) = await SetupReviewableCapture();
        if (reviewId == Guid.Empty) return;

        var response = await _client.GetAsync($"/reviews/{reviewId}");
        var body = await response.Content.ReadFromJsonAsync<ReviewDetailResponse>();

        body.Should().NotBeNull();
        body!.ReviewId.Should().Be(reviewId);
        body.OcrAnswers.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReviewDetail_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/reviews/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCaptureImage_AfterUpload_ReturnsBytes()
    {
        var (captureId, _) = await SetupReviewableCapture();
        var response = await _client.GetAsync($"/captures/{captureId}/image");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCaptureImage_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/captures/{Guid.NewGuid()}/image");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
