using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class UserActiveStatusFilterTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() =>
        _client = await factory.CreateAuthenticatedClientAsync();

    public Task DisposeAsync() { _client.Dispose(); return Task.CompletedTask; }

    private async Task<Guid> CreateUserAsync(string email)
    {
        var res = await _client.PostAsJsonAsync("/auth/users",
            new CreateUserRequest(email, "P@ssword123!", "Operator"));
        var dto = await res.Content.ReadFromJsonAsync<CreateUserResponse>();
        return dto!.UserId;
    }

    [Fact]
    public async Task GetUsers_WithIsActiveTrue_ReturnsOnlyActiveUsers()
    {
        var activeId = await CreateUserAsync("active-filter@example.com");
        var inactiveId = await CreateUserAsync("inactive-filter@example.com");
        await _client.PutAsync($"/users/{inactiveId}/deactivate", null);

        var res  = await _client.GetAsync("/users?isActive=true");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<UserListResponse>();
        var ids  = body!.Users.Select(u => u.UserId).ToHashSet();

        Assert.Contains(activeId, ids);
        Assert.DoesNotContain(inactiveId, ids);
        Assert.All(body.Users, u => Assert.True(u.IsActive));
    }

    [Fact]
    public async Task GetUsers_WithIsActiveFalse_ReturnsOnlyInactiveUsers()
    {
        var activeId   = await CreateUserAsync("active-only@example.com");
        var inactiveId = await CreateUserAsync("inactive-only@example.com");
        await _client.PutAsync($"/users/{inactiveId}/deactivate", null);

        var res  = await _client.GetAsync("/users?isActive=false");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<UserListResponse>();
        var ids  = body!.Users.Select(u => u.UserId).ToHashSet();

        Assert.Contains(inactiveId, ids);
        Assert.DoesNotContain(activeId, ids);
        Assert.All(body.Users, u => Assert.False(u.IsActive));
    }

    [Fact]
    public async Task GetUsers_WithNoIsActiveFilter_ReturnsBothActiveAndInactive()
    {
        var activeId   = await CreateUserAsync("both-active@example.com");
        var inactiveId = await CreateUserAsync("both-inactive@example.com");
        await _client.PutAsync($"/users/{inactiveId}/deactivate", null);

        var res  = await _client.GetAsync("/users");
        var body = await res.Content.ReadFromJsonAsync<UserListResponse>();
        var ids  = body!.Users.Select(u => u.UserId).ToHashSet();

        Assert.Contains(activeId,   ids);
        Assert.Contains(inactiveId, ids);
    }

    [Fact]
    public async Task GetUsers_IsActiveFalse_CanBeCombinedWithRoleFilter()
    {
        var userId = await CreateUserAsync("inactive-operator@example.com");
        await _client.PutAsync($"/users/{userId}/deactivate", null);

        var res  = await _client.GetAsync("/users?isActive=false&role=Operator");
        var body = await res.Content.ReadFromJsonAsync<UserListResponse>();

        Assert.Contains(userId, body!.Users.Select(u => u.UserId));
        Assert.All(body.Users, u => Assert.False(u.IsActive));
    }
}
