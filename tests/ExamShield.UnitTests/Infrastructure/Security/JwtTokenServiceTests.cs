using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace ExamShield.UnitTests.Infrastructure.Security;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService BuildSut(string secret = "super-secret-key-that-is-at-least-32-chars!") =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Jwt:Secret", secret),
                new("Jwt:Issuer", "TestIssuer"),
                new("Jwt:Audience", "TestAudience"),
                new("Jwt:ExpirationMinutes", "60"),
            ])
            .Build());

    private static User MakeUser() =>
        User.Create(new Email("alice@exam.io"), "hash", UserRole.Invigilator);

    [Fact]
    public void Generate_ReturnsNonEmptyToken()
    {
        var token = BuildSut().Generate(MakeUser());
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Generate_TokenIsValidJwt()
    {
        var token = BuildSut().Generate(MakeUser());
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void Generate_ClaimsContainUserEmailAndRole()
    {
        var user = MakeUser();
        var token = BuildSut().Generate(user);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "alice@exam.io");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Invigilator");
    }

    [Fact]
    public void Generate_ClaimsContainUserIdAsSub()
    {
        var user = MakeUser();
        var token = BuildSut().Generate(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.Value.ToString());
    }

    [Fact]
    public void Generate_TokenHasConfiguredIssuerAndAudience()
    {
        var token = BuildSut().Generate(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void Constructor_MissingSecret_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var act = () => new JwtTokenService(config);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Secret*");
    }

    [Fact]
    public void Generate_TwoCalls_ProduceDifferentJtiClaims()
    {
        var sut = BuildSut();
        var user = MakeUser();
        var handler = new JwtSecurityTokenHandler();

        var jti1 = handler.ReadJwtToken(sut.Generate(user)).Claims
            .First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = handler.ReadJwtToken(sut.Generate(user)).Claims
            .First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }
}
