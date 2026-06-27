using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamSchedulingTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private async Task<HttpClient> AdminClientAsync() =>
        await factory.CreateAuthenticatedClientAsync();

    [Fact]
    public async Task CreateExam_WithSchedule_ReturnsScheduleInResponse()
    {
        var client     = await AdminClientAsync();
        var scheduled  = DateTimeOffset.UtcNow.AddDays(7);
        var ends       = scheduled.AddHours(3);

        var res = await client.PostAsJsonAsync("/exams",
            new CreateExamRequest("Scheduled Exam", null, 30, scheduled, ends));

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ExamResponse>();
        Assert.NotNull(body!.ScheduledAt);
        Assert.NotNull(body.EndsAt);
        Assert.Equal(scheduled.ToUnixTimeSeconds(), body.ScheduledAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(ends.ToUnixTimeSeconds(),      body.EndsAt!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task CreateExam_WithoutSchedule_ReturnsNullSchedule()
    {
        var client = await AdminClientAsync();

        var res = await client.PostAsJsonAsync("/exams",
            new CreateExamRequest("Unscheduled Exam", null, 20));

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ExamResponse>();
        Assert.Null(body!.ScheduledAt);
        Assert.Null(body.EndsAt);
    }

    [Fact]
    public async Task CreateExam_EndsAtBeforeScheduledAt_Returns400()
    {
        var client    = await AdminClientAsync();
        var scheduled = DateTimeOffset.UtcNow.AddDays(7);
        var ends      = scheduled.AddHours(-1); // before start

        var res = await client.PostAsJsonAsync("/exams",
            new CreateExamRequest("Bad Schedule", null, 20, scheduled, ends));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ListExams_ScheduleFieldsPresentOnAllItems()
    {
        var client    = await AdminClientAsync();
        var scheduled = DateTimeOffset.UtcNow.AddDays(5);
        var ends      = scheduled.AddHours(2);

        await client.PostAsJsonAsync("/exams",
            new CreateExamRequest("Listed Scheduled", null, 10, scheduled, ends));

        var listRes = await client.GetAsync("/exams");
        listRes.EnsureSuccessStatusCode();
        var list = await listRes.Content.ReadFromJsonAsync<ExamListResponse>();

        Assert.NotEmpty(list!.Exams);
        // At least one exam in the list has the schedule we just created
        var found = list.Exams.FirstOrDefault(e => e.Name == "Listed Scheduled");
        Assert.NotNull(found);
        Assert.NotNull(found!.ScheduledAt);
        Assert.NotNull(found.EndsAt);
    }

    [Fact]
    public async Task CreateExam_WithScheduledAtOnly_EndsAtIsNull()
    {
        var client    = await AdminClientAsync();
        var scheduled = DateTimeOffset.UtcNow.AddDays(3);

        var res = await client.PostAsJsonAsync("/exams",
            new CreateExamRequest("Open-ended Exam", null, 15, scheduled, null));

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ExamResponse>();
        Assert.NotNull(body!.ScheduledAt);
        Assert.Null(body.EndsAt);
    }
}
