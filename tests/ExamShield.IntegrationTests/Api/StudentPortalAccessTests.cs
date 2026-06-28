using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;

namespace ExamShield.IntegrationTests.Api;

public sealed class StudentPortalAccessTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private sealed record CreateUserReq(string Email, string Password, string Role);
    private sealed record LoginReq(string Email, string Password);
    private sealed record LoginResp(string Token, string RefreshToken, string Role);

    private async Task<(HttpClient Client, Guid StudentId)> CreateStudentClientAsync()
    {
        using var admin = await factory.CreateAuthenticatedClientAsync();
        var email = $"student-{Guid.NewGuid():N}@test.com";
        const string password = "StudentPass1!";
        await admin.PostAsJsonAsync("/auth/users", new CreateUserReq(email, password, "Student"));

        var anon = factory.CreateClient();
        var loginRes = await anon.PostAsJsonAsync("/auth/login", new LoginReq(email, password));
        var login = await loginRes.Content.ReadFromJsonAsync<LoginResp>();

        anon.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.Token);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(login.Token);
        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        return (anon, Guid.Parse(sub));
    }

    [Fact]
    public async Task GetStudentResults_AsStudentRole_Returns200NotForbidden()
    {
        var (client, studentId) = await CreateStudentClientAsync();
        using (client)
        {
            var res = await client.GetAsync($"/student/results?studentId={studentId}");
            Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, res.StatusCode);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    [Fact]
    public async Task GetStudentReviewRequests_AsStudentRole_Returns200NotForbidden()
    {
        var (client, studentId) = await CreateStudentClientAsync();
        using (client)
        {
            var res = await client.GetAsync($"/student/review-requests?studentId={studentId}");
            Assert.NotEqual(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }
}
