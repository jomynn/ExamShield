using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Helpers;

public static class ExamHelper
{
    public static async Task<Guid> CreateExamAsync(HttpClient client, string name, int totalQuestions = 10)
    {
        var response = await client.PostAsJsonAsync("/exams/", new CreateExamRequest(name, null, totalQuestions));
        response.EnsureSuccessStatusCode();
        var exam = await response.Content.ReadFromJsonAsync<ExamResponse>();
        return exam!.ExamId;
    }
}
