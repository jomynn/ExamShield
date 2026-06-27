using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamDateRangeFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateScheduledExamAsync(string name, DateTimeOffset scheduledAt)
    {
        var res = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest(name, null, 5, scheduledAt, scheduledAt.AddHours(3)));
        var dto = await res.Content.ReadFromJsonAsync<ExamResponse>();
        return dto!.ExamId;
    }

    [Fact]
    public async Task GetExams_WithScheduledFrom_ExcludesExamsBefore()
    {
        var past   = DateTimeOffset.UtcNow.AddDays(-30);
        var future = DateTimeOffset.UtcNow.AddDays(30);

        var pastId   = await CreateScheduledExamAsync("Past Exam",   past);
        var futureId = await CreateScheduledExamAsync("Future Exam", future);

        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var res  = await _client.GetAsync($"/exams/?scheduledFrom={Uri.EscapeDataString(from)}");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<ExamListResponse>();
        var ids  = body!.Exams.Select(e => e.ExamId).ToHashSet();

        Assert.Contains(futureId, ids);
        Assert.DoesNotContain(pastId, ids);
    }

    [Fact]
    public async Task GetExams_WithScheduledTo_ExcludesExamsAfter()
    {
        var near = DateTimeOffset.UtcNow.AddDays(1);
        var far  = DateTimeOffset.UtcNow.AddDays(90);

        var nearId = await CreateScheduledExamAsync("Near Exam", near);
        var farId  = await CreateScheduledExamAsync("Far Exam",  far);

        var to  = DateTimeOffset.UtcNow.AddDays(10).ToString("O");
        var res = await _client.GetAsync($"/exams/?scheduledTo={Uri.EscapeDataString(to)}");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<ExamListResponse>();
        var ids  = body!.Exams.Select(e => e.ExamId).ToHashSet();

        Assert.Contains(nearId, ids);
        Assert.DoesNotContain(farId, ids);
    }

    [Fact]
    public async Task GetExams_WithBothDateBounds_ReturnsOnlyExamsInRange()
    {
        var inRange  = DateTimeOffset.UtcNow.AddDays(5);
        var outRange = DateTimeOffset.UtcNow.AddDays(60);

        var inId  = await CreateScheduledExamAsync("InRange Exam",  inRange);
        var outId = await CreateScheduledExamAsync("OutRange Exam", outRange);

        var from = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var to   = DateTimeOffset.UtcNow.AddDays(30).ToString("O");
        var res  = await _client.GetAsync(
            $"/exams/?scheduledFrom={Uri.EscapeDataString(from)}&scheduledTo={Uri.EscapeDataString(to)}");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<ExamListResponse>();
        var ids  = body!.Exams.Select(e => e.ExamId).ToHashSet();

        Assert.Contains(inId, ids);
        Assert.DoesNotContain(outId, ids);
    }

    [Fact]
    public async Task GetExams_NoDateFilter_ReturnsAll()
    {
        var res  = await _client.GetAsync("/exams/");
        var body = await res.Content.ReadFromJsonAsync<ExamListResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Exams);
    }

    [Fact]
    public async Task GetExams_ExamWithNoScheduledAt_ExcludedFromDateRangeFilter()
    {
        // Create an exam with no scheduledAt
        var unscheduledRes = await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest("Unscheduled", null, 3));
        var unscheduled = await unscheduledRes.Content.ReadFromJsonAsync<ExamResponse>();

        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var res  = await _client.GetAsync($"/exams/?scheduledFrom={Uri.EscapeDataString(from)}");
        var body = await res.Content.ReadFromJsonAsync<ExamListResponse>();
        var ids  = body!.Exams.Select(e => e.ExamId).ToHashSet();

        Assert.DoesNotContain(unscheduled!.ExamId, ids);
    }
}
