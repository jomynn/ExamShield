using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class ExamUpdateTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateDraftAsync(string name = "Update Test") =>
        (await (await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest(name, null, 10)))
        .Content.ReadFromJsonAsync<ExamResponse>())!.ExamId;

    [Fact]
    public async Task UpdateExam_DraftExam_Returns204()
    {
        var id = await CreateDraftAsync();

        var res = await _client.PutAsJsonAsync($"/exams/{id}",
            new UpdateExamRequest("Renamed", "New desc", null, null));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_ChangedNameAppearsInList()
    {
        var id = await CreateDraftAsync("Before Rename");

        await _client.PutAsJsonAsync($"/exams/{id}",
            new UpdateExamRequest("After Rename", null, null, null));

        var list = await (await _client.GetAsync("/exams/"))
            .Content.ReadFromJsonAsync<ExamListResponse>();
        var exam = list!.Exams.FirstOrDefault(e => e.ExamId == id);
        Assert.Equal("After Rename", exam?.Name);
    }

    [Fact]
    public async Task UpdateExam_ActiveExam_Returns422()
    {
        var id = await CreateDraftAsync();
        await _client.PutAsync($"/exams/{id}/activate", null);

        var res = await _client.PutAsJsonAsync($"/exams/{id}",
            new UpdateExamRequest("Cannot rename active", null, null, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_EndsAtBeforeScheduledAt_Returns400()
    {
        var id   = await CreateDraftAsync();
        var base_ = DateTimeOffset.UtcNow.AddDays(5);

        var res = await _client.PutAsJsonAsync($"/exams/{id}",
            new UpdateExamRequest("X", null, base_.AddDays(2), base_));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_UnknownId_Returns404()
    {
        var res = await _client.PutAsJsonAsync($"/exams/{Guid.NewGuid()}",
            new UpdateExamRequest("Ghost", null, null, null));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UpdateExam_Unauthenticated_Returns401()
    {
        var id  = await CreateDraftAsync();
        var res = await factory.CreateClient().PutAsJsonAsync($"/exams/{id}",
            new UpdateExamRequest("Anon", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
