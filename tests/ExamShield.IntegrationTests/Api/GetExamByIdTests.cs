using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class GetExamByIdTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<ExamResponse> CreateExamAsync(string name = "Detail Test") =>
        (await (await _client.PostAsJsonAsync("/exams/",
            new CreateExamRequest(name, "Some desc", 30)))
        .Content.ReadFromJsonAsync<ExamResponse>())!;

    [Fact]
    public async Task GetExamById_ExistingId_Returns200WithDetails()
    {
        var created = await CreateExamAsync();

        var res  = await _client.GetAsync($"/exams/{created.ExamId}");
        var exam = await res.Content.ReadFromJsonAsync<ExamResponse>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(created.ExamId, exam!.ExamId);
        Assert.Equal("Detail Test", exam.Name);
        Assert.Equal("Some desc", exam.Description);
        Assert.Equal(30, exam.TotalQuestions);
        Assert.Equal("Draft", exam.Status);
    }

    [Fact]
    public async Task GetExamById_UnknownId_Returns404()
    {
        var res = await _client.GetAsync($"/exams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetExamById_Unauthenticated_Returns401()
    {
        var created = await CreateExamAsync();
        var res     = await factory.CreateClient().GetAsync($"/exams/{created.ExamId}");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
