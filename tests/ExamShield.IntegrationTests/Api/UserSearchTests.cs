using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;

namespace ExamShield.IntegrationTests.Api;

public sealed class UserSearchTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetUsers_NoFilter_ReturnsAll()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/users/?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserListResponse>();
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
    }

    [Fact]
    public async Task GetUsers_SearchByEmail_ReturnsMatchingUsers()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var unique = $"search-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register", new { Email = unique, Password = "Pass@1234!", Role = "Auditor" });

        var response = await client.GetAsync($"/users/?search={Uri.EscapeDataString(unique)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserListResponse>();
        Assert.NotNull(body);
        Assert.All(body!.Users, u => Assert.Contains(unique, u.Email));
    }

    [Fact]
    public async Task GetUsers_FilterByRole_ReturnsOnlyThatRole()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/users/?role=Administrator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserListResponse>();
        Assert.NotNull(body);
        Assert.All(body!.Users, u => Assert.Equal("Administrator", u.Role));
    }

    [Fact]
    public async Task GetUsers_FilterByUnknownRole_ReturnsEmptyList()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/users/?role=NonExistentRole");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserListResponse>();
        Assert.NotNull(body);
        Assert.Empty(body!.Users);
    }

    [Fact]
    public async Task GetUsers_Unauthenticated_Returns401()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/users/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
