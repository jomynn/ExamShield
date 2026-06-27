using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class AdminReviewRequestQueueTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetAllReviewRequests_NoFilter_ReturnsOk()
    {
        var res = await _client.GetAsync("/admin/review-requests");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetAllReviewRequests_ContainsSubmittedRequest()
    {
        var captureId = await TestHelpers.RegisterCaptureAsync(_client, factory.ActiveExamId);
        await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(captureId, Guid.NewGuid(), "OCR misread Q3."));

        var res  = await _client.GetAsync("/admin/review-requests?status=Pending");
        var body = await res.Content.ReadFromJsonAsync<AllReviewRequestsResponse>();

        Assert.NotNull(body);
        Assert.Contains(body.Items, r => r.CaptureId == captureId);
    }

    [Fact]
    public async Task GetAllReviewRequests_StatusFilter_ExcludesOtherStatuses()
    {
        var captureId = await TestHelpers.RegisterCaptureAsync(_client, factory.ActiveExamId);
        var submitRes = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(captureId, Guid.NewGuid(), "Question 7 was marked wrong."));
        var submitBody = await submitRes.Content.ReadFromJsonAsync<SubmitReviewRequestResponse>();

        await _client.PutAsJsonAsync(
            $"/student/review-requests/{submitBody!.ReviewRequestId}/resolve",
            new ProcessReviewRequestBody("Verified correct."));

        var res  = await _client.GetAsync("/admin/review-requests?status=Pending");
        var body = await res.Content.ReadFromJsonAsync<AllReviewRequestsResponse>();

        Assert.NotNull(body);
        Assert.DoesNotContain(body.Items, r => r.ReviewRequestId == submitBody.ReviewRequestId);
    }

    [Fact]
    public async Task GetAllReviewRequests_Unauthenticated_Returns401()
    {
        var res = await factory.CreateClient().GetAsync("/admin/review-requests");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
