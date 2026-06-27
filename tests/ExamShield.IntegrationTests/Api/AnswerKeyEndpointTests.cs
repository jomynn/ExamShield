using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class AnswerKeyEndpointTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private Guid _examId;

    public AnswerKeyEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _client = await _factory.CreateAuthenticatedClientAsync();
        _examId = _factory.ActiveExamId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SetAnswerKey_WithValidAnswers_Returns204()
    {
        var body = new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" });

        var response = await _client.PostAsJsonAsync($"/exams/{_examId}/answer-key", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SetAnswerKey_WhenAlreadySet_Returns409()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Key Test Exam", null, 3));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var body = new SetAnswerKeyRequest(new Dictionary<int, string> { [1] = "A" });
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/answer-key", body);

        var secondAttempt = await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/answer-key", body);

        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task SetAnswerKey_WithEmptyAnswers_Returns400()
    {
        var body = new SetAnswerKeyRequest(new Dictionary<int, string>());

        var response = await _client.PostAsJsonAsync($"/exams/{_examId}/answer-key", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAnswerKey_WhenSet_Returns200WithAnswers()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("Key Fetch Exam", null, 2));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var answers = new Dictionary<int, string> { [1] = "A", [2] = "C" };
        await _client.PostAsJsonAsync($"/exams/{exam.ExamId}/answer-key", new SetAnswerKeyRequest(answers));

        var response = await _client.GetAsync($"/exams/{exam.ExamId}/answer-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AnswerKeyResponse>();
        Assert.Equal(2, result!.Answers.Count);
        Assert.Equal("A", result.Answers["1"]);
        Assert.Equal("C", result.Answers["2"]);
    }

    [Fact]
    public async Task GetAnswerKey_WhenNotSet_Returns404()
    {
        var examRes = await _client.PostAsJsonAsync("/exams/", new CreateExamRequest("No Key Exam", null, 5));
        var exam = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        await _client.PutAsync($"/exams/{exam!.ExamId}/activate", null);

        var response = await _client.GetAsync($"/exams/{exam.ExamId}/answer-key");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
