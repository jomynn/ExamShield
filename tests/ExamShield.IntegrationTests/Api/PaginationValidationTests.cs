using System.Net;

namespace ExamShield.IntegrationTests.Api;

public sealed class PaginationValidationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Theory]
    [InlineData("/captures?page=0")]
    [InlineData("/captures?page=-1")]
    [InlineData("/captures?pageSize=0")]
    [InlineData("/captures?pageSize=-1")]
    [InlineData("/captures?pageSize=201")]
    [InlineData("/audit?page=0")]
    [InlineData("/audit?pageSize=0")]
    [InlineData("/users?page=0")]
    [InlineData("/users?pageSize=0")]
    public async Task Get_WithInvalidPagination_Returns400(string url)
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Theory]
    [InlineData("/captures?page=1&pageSize=1")]
    [InlineData("/captures?page=1&pageSize=200")]
    [InlineData("/audit?page=1&pageSize=50")]
    public async Task Get_WithValidPagination_Returns200(string url)
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
