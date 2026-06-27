using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

public sealed class StudentReviewRequestEndpointTests(TestWebApplicationFactory factory)
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

    [Fact]
    public async Task PostReviewRequest_WithValidBody_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, "OCR misread question 5"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostReviewRequest_WithValidBody_ReturnsReviewRequestId()
    {
        var response = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, "Ink smudge caused wrong answer"));

        var body = await response.Content.ReadFromJsonAsync<SubmitReviewRequestResponse>();
        body!.ReviewRequestId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PostReviewRequest_WithUnknownCapture_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(Guid.NewGuid(), _studentId, "Unknown capture"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostReviewRequest_WithEmptyReason_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostReviewRequest_Unauthenticated_Returns401()
    {
        using var anon = factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, "some reason"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostReviewRequest_WithWrongStudentId_Returns403()
    {
        var response = await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, Guid.NewGuid(), "Trying to claim someone else's capture"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetReviewRequests_ReturnsPreviouslySubmittedRequest()
    {
        await _client.PostAsJsonAsync("/student/review-request",
            new SubmitReviewRequestBody(_captureId, _studentId, "Paper was wet, ink ran"));

        var response = await _client.GetAsync($"/student/review-requests?studentId={_studentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReviewRequestListResponse>();
        body!.Items.Should().ContainSingle(r => r.StudentId == _studentId);
    }
}
