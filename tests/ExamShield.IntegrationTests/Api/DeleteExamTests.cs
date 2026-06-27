using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class DeleteExamTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateDraftAsync(string name = "To Delete") =>
        (await (await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest(name, null, 10)))
        .Content.ReadFromJsonAsync<ExamResponse>())!.ExamId;

    [Fact]
    public async Task DeleteExam_DraftExam_Returns204()
    {
        var id  = await CreateDraftAsync();
        var res = await _client.DeleteAsync($"/exams/{id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_DeletedExamDisappearsFromList()
    {
        var id = await CreateDraftAsync("Disappear Test");
        await _client.DeleteAsync($"/exams/{id}");

        var list = await (await _client.GetAsync("/exams/"))
            .Content.ReadFromJsonAsync<ExamListResponse>();
        Assert.DoesNotContain(list!.Exams, e => e.ExamId == id);
    }

    [Fact]
    public async Task DeleteExam_DeletedExamReturns404OnGetById()
    {
        var id = await CreateDraftAsync();
        await _client.DeleteAsync($"/exams/{id}");

        var res = await _client.GetAsync($"/exams/{id}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_ActiveExam_Returns422()
    {
        var id = await CreateDraftAsync();
        await _client.PutAsync($"/exams/{id}/activate", null);

        var res = await _client.DeleteAsync($"/exams/{id}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_UnknownId_Returns404()
    {
        var res = await _client.DeleteAsync($"/exams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task DeleteExam_Unauthenticated_Returns401()
    {
        var id  = await CreateDraftAsync();
        var res = await factory.CreateClient().DeleteAsync($"/exams/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
