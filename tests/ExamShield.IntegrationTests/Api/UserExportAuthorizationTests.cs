using System.Net;
using ExamShield.Domain.Enums;
using ExamShield.IntegrationTests.Helpers;
using FluentAssertions;

namespace ExamShield.IntegrationTests.Api;

public sealed class UserExportAuthorizationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Export_AsAuditor_Returns403()
    {
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Auditor);

        var res = await client.GetAsync("/users/export");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_AsAdministrator_Returns200()
    {
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Administrator);

        var res = await client.GetAsync("/users/export");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_AsOperator_Returns403()
    {
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operator);

        var res = await client.GetAsync("/users/export");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
