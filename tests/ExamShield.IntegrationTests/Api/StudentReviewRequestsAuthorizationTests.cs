using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class StudentReviewRequestsAuthorizationTests
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StudentReviewRequestsAuthorizationTests(TestWebApplicationFactory factory) =>
        _factory = factory;

    private sealed record CreateUserReq(string Email, string Password, string Role);
    private sealed record LoginReq(string Email, string Password);
    private sealed record LoginResp(string Token, string RefreshToken, string Role);

    private async Task<(HttpClient Client, Guid StudentId)> CreateStudentClientAsync()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync();
        var email = $"rr-student-{Guid.NewGuid():N}@test.com";
        const string password = "StudentPass1!";
        await admin.PostAsJsonAsync("/auth/users", new CreateUserReq(email, password, "Student"));

        var anon = _factory.CreateClient();
        var loginRes = await anon.PostAsJsonAsync("/auth/login", new LoginReq(email, password));
        var login = await loginRes.Content.ReadFromJsonAsync<LoginResp>();
        anon.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.Token);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        return (anon, Guid.Parse(sub));
    }

    [Fact]
    public async Task GetReviewRequests_WhenStudentRequestsOwnList_Returns200()
    {
        var (client, studentId) = await CreateStudentClientAsync();
        using (client)
        {
            var res = await client.GetAsync($"/student/review-requests?studentId={studentId}");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetReviewRequests_WhenStudentRequestsAnotherStudentsList_Returns403()
    {
        var (client, _) = await CreateStudentClientAsync();
        using (client)
        {
            var otherId = Guid.NewGuid();
            var res = await client.GetAsync($"/student/review-requests?studentId={otherId}");
            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
