using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ReviewRequestResolutionTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;
    private Guid _captureId;
    private Guid _studentId;

    public async Task InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        (_captureId, _studentId) = await TestHelpers.RegisterCaptureAsync(_client, factory.ActiveExamId);
    }

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> SubmitRequestAsync()
    {
        var res = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, "My answer was marked wrong."));
        var body = await res.Content.ReadFromJsonAsync<SubmitReviewRequestResponse>();
        return body!.ReviewRequestId;
    }

    [Fact]
    public async Task Resolve_PendingRequest_Returns204()
    {
        var id = await SubmitRequestAsync();

        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/resolve",
            new ProcessReviewRequestBody("Score corrected after review."));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Reject_PendingRequest_Returns204()
    {
        var id = await SubmitRequestAsync();

        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/reject",
            new ProcessReviewRequestBody("No error found in OCR."));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Resolve_AlreadyResolved_Returns422()
    {
        var id = await SubmitRequestAsync();
        await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/resolve",
            new ProcessReviewRequestBody("First resolution."));

        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/resolve",
            new ProcessReviewRequestBody("Second resolution."));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Reject_AlreadyRejected_Returns422()
    {
        var id = await SubmitRequestAsync();
        await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/reject",
            new ProcessReviewRequestBody("Not valid."));

        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{id}/reject",
            new ProcessReviewRequestBody("Again."));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Resolve_UnknownId_Returns404()
    {
        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{Guid.NewGuid()}/resolve",
            new ProcessReviewRequestBody("Note."));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Reject_UnknownId_Returns404()
    {
        var res = await _client.PutAsJsonAsync(
            $"/student/review-requests/{Guid.NewGuid()}/reject",
            new ProcessReviewRequestBody("Reason."));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
