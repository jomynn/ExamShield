using System.Net;
using ExamShield.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ExamShield.IntegrationTests.Api;

public sealed class StudentResultsAuthorizationTests
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StudentResultsAuthorizationTests(TestWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task GetStudentResults_WhenStudentRequestsOwnResults_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRole.Student);
        // The student user's ID is encoded in their JWT sub claim.
        // Fetch the student ID from the /users/me equivalent — we'll derive it from
        // the user that CreateAuthenticatedClientAsync creates for the Student role.
        var studentEmail = "test-student@examshield.test";

        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider
            .GetRequiredService<ExamShield.Domain.Interfaces.IUserRepository>();
        var user = await userRepo.FindByEmailAsync(
            new ExamShield.Domain.ValueObjects.Email(studentEmail));
        var studentId = user!.Id.Value;

        var res = await client.GetAsync($"/student/results?studentId={studentId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStudentResults_WhenStudentRequestsAnotherStudentsResults_Returns403()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRole.Student);
        var otherId = Guid.NewGuid(); // a different student's ID

        var res = await client.GetAsync($"/student/results?studentId={otherId}");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
