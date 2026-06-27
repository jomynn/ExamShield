using System.Net;
using System.Net.Http.Json;

namespace ExamShield.IntegrationTests.Api;

public sealed class AccountLockoutTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string AccessToken, string RefreshToken, string Role);
    private sealed record CreateUserRequest(string Email, string Password, string Role);

    [Fact]
    public async Task Login_AfterFiveFailures_IsLockedOut()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var email = $"lockout-{Guid.NewGuid():N}@test.com";
        await client.PostAsJsonAsync("/auth/users", new CreateUserRequest(email, "CorrectPass1!", "Operator"));

        using var anonClient = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var res = await anonClient.PostAsJsonAsync("/auth/login",
                new LoginRequest(email, "WrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        var lockedRes = await anonClient.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "CorrectPass1!"));
        Assert.Equal(HttpStatusCode.Unauthorized, lockedRes.StatusCode);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsAttemptCounter()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var email = $"reset-{Guid.NewGuid():N}@test.com";
        const string password = "ResetPass1!";
        await client.PostAsJsonAsync("/auth/users", new CreateUserRequest(email, password, "Operator"));

        using var anonClient = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await anonClient.PostAsJsonAsync("/auth/login",
                new LoginRequest(email, "WrongPassword!"));
        }

        var okRes = await anonClient.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);

        var stillOk = await anonClient.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, stillOk.StatusCode);
    }
}
