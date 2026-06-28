using System.Net;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.Login;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Realtime;
using ExamShield.Infrastructure.Security;
using ExamShield.IntegrationTests.Fakes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace ExamShield.IntegrationTests.Api;

/// <summary>
/// End-to-end tests that verify MFA is enforced for privileged roles.
/// Uses a separate factory that pre-enables MFA on the admin seed user
/// and turns on EnforceMfaForPrivilegedRoles so the full login flow can
/// be exercised without a chicken-and-egg auth dependency.
/// </summary>
public sealed class MfaEnforcementIntegrationTests : IClassFixture<MfaEnforcedWebApplicationFactory>
{
    private readonly MfaEnforcedWebApplicationFactory _factory;
    private readonly TotpService _totp = new();

    public MfaEnforcementIntegrationTests(MfaEnforcedWebApplicationFactory factory)
        => _factory = factory;

    // ── Phase A: enforcement on, MFA not set up ────────────────────────

    [Fact]
    public async Task Login_WithEnforcement_WhenPrivilegedUserHasNoMfa_ReturnsMfaSetupRequired()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(MfaEnforcedWebApplicationFactory.NoMfaEmail,
                             MfaEnforcedWebApplicationFactory.NoMfaPassword));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        body!.MfaSetupRequired.Should().BeTrue("privileged role must set up MFA when enforcement is on");
        body.Token.Should().BeNullOrWhiteSpace("no token until MFA is set up");
        body.RequiresMfa.Should().BeFalse();
    }

    // ── Phase B: enforcement on, MFA already set up ────────────────────

    [Fact]
    public async Task Login_WithEnforcement_WhenMfaIsSetUp_AndNoCode_ReturnsRequiresMfa()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(MfaEnforcedWebApplicationFactory.AdminEmail,
                             MfaEnforcedWebApplicationFactory.AdminPassword));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        body!.RequiresMfa.Should().BeTrue("MFA is enabled — code required");
        body.Token.Should().BeNullOrWhiteSpace("no full token without TOTP code");
        body.MfaSetupRequired.Should().BeFalse();
    }

    // ── Phase C: enforcement on, MFA set up, valid TOTP code ──────────

    [Fact]
    public async Task MfaLogin_WithEnforcement_WithValidTotpCode_ReturnsRealJwt()
    {
        using var client = _factory.CreateClient();
        var code = _totp.GenerateCurrentCode(MfaEnforcedWebApplicationFactory.KnownMfaSecret);

        var res = await client.PostAsJsonAsync("/auth/mfa/login",
            new MfaLoginRequest(
                MfaEnforcedWebApplicationFactory.AdminEmail,
                MfaEnforcedWebApplicationFactory.AdminPassword,
                code));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrWhiteSpace("valid TOTP code should yield a real JWT");
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.RequiresMfa.Should().BeFalse();
        body.MfaSetupRequired.Should().BeFalse();
    }

    // ── Negative: wrong TOTP code is still rejected under enforcement ──

    [Fact]
    public async Task MfaLogin_WithEnforcement_WithInvalidCode_Returns401()
    {
        using var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/auth/mfa/login",
            new MfaLoginRequest(
                MfaEnforcedWebApplicationFactory.AdminEmail,
                MfaEnforcedWebApplicationFactory.AdminPassword,
                "000000"));

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

private sealed record MfaSetupDto(string Secret, string QrUri);
}

// ────────────────────────────────────────────────────────────────────────────
// Custom factory: enforcement ON; one user with MFA already enabled (Phase B/C),
// plus a second user without MFA (Phase A).
// ────────────────────────────────────────────────────────────────────────────

public sealed class MfaEnforcedWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail    = "admin-enforced@test.examshield";
    public const string AdminPassword = "Test@1234!Admin";

    // A well-known base-32 TOTP secret so tests can generate valid codes.
    public const string KnownMfaSecret = "JBSWY3DPEHPK3PXP";

    // Admin with MFA enabled (for Phases B and C).
    private readonly User _adminWithMfa;

    // Separate admin account with NO MFA (for Phase A — MfaSetupRequired path).
    public const string NoMfaEmail    = "admin-nomfa@test.examshield";
    public const string NoMfaPassword = "Test@1234!Admin";
    private readonly User _adminNoMfa;

    public MfaEnforcedWebApplicationFactory()
    {
        _adminWithMfa = User.Create(
            new Email(AdminEmail),
            BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: 4),
            UserRole.Administrator);
        _adminWithMfa.SetMfaSecret(KnownMfaSecret);
        _adminWithMfa.EnableMfa();

        _adminNoMfa = User.Create(
            new Email(NoMfaEmail),
            BCrypt.Net.BCrypt.HashPassword(NoMfaPassword, workFactor: 4),
            UserRole.Administrator);
        // MFA NOT enabled — triggers MfaSetupRequired when enforcement is on.
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // LoginOptions is registered as a concrete singleton in Program.cs before
            // ConfigureAppConfiguration can influence it, so override it here directly.
            services.RemoveAll<LoginOptions>();
            services.AddSingleton(new LoginOptions { EnforceMfaForPrivilegedRoles = true });

            // Seed BOTH admin users into one in-memory repository.
            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(
                new InMemoryUserRepository(seed: [_adminWithMfa, _adminNoMfa]));

            // Minimal fakes so the server can start (no real DB / SMTP / etc.)
            services.RemoveAll<ICaptureRepository>();
            services.AddSingleton<ICaptureRepository, InMemoryCaptureRepository>();

            services.RemoveAll<IAuditLogRepository>();
            services.AddSingleton<IAuditLogRepository>(sp =>
                new InMemoryAuditLogRepository(sp.GetRequiredService<IServerSigningService>()));

            services.RemoveAll<IRefreshTokenRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();

            services.RemoveAll<ISecurityEventRepository>();
            services.AddSingleton<ISecurityEventRepository, InMemorySecurityEventRepository>();

            services.RemoveAll<ITotpUsedCodeCache>();
            services.AddSingleton<ITotpUsedCodeCache, Fakes.InMemoryTotpUsedCodeCache>();

            services.RemoveAll<IAlertService>();
            services.AddSingleton<IAlertService, NullAlertService>();

            services.RemoveAll<IRealtimeNotificationService>();
            services.AddSingleton<IRealtimeNotificationService, NullRealtimeNotificationService>();

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, NullEmailSender>();

            services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        });
    }
}
