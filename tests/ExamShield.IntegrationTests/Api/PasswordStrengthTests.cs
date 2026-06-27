using System.Net;
using System.Net.Http.Json;

namespace ExamShield.IntegrationTests.Api;

public sealed class PasswordStrengthTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    [Theory]
    [InlineData("short")]         // too short
    [InlineData("alllowercase1!")]   // no uppercase
    [InlineData("ALLUPPERCASE1!")]   // no lowercase
    [InlineData("NoDigits!Here")]    // no digit
    [InlineData("NoSpecial1Chars")]  // no special char
    public async Task CreateUser_WeakPassword_Returns400(string weakPassword)
    {
        var res = await _client.PostAsJsonAsync("/auth/users", new
        {
            email      = $"user_{Guid.NewGuid():N}@test.com",
            password   = weakPassword,
            role       = "Auditor",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CreateUser_StrongPassword_Returns201()
    {
        var res = await _client.PostAsJsonAsync("/auth/users", new
        {
            email    = $"user_{Guid.NewGuid():N}@test.com",
            password = "Str0ng!Password",
            role     = "Auditor",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400()
    {
        // Create a user with a known password
        var email = $"pw_{Guid.NewGuid():N}@test.com";
        var create = await _client.PostAsJsonAsync("/auth/users", new
        {
            email, password = "Init1al!Pass", role = "Auditor",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreateUserResponse>();

        var res = await _client.PostAsJsonAsync("/auth/password/change", new
        {
            userId          = created!.UserId,
            currentPassword = "Init1al!Pass",
            newPassword     = "weakpass",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private sealed record CreateUserResponse(Guid UserId);
}
