using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExamShield.Api.Contracts;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Realtime;
using ExamShield.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExamShield.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.examshield";
    public const string AdminPassword = "Test@1234!Admin";

    // Instance field so each factory gets a fresh user object — static would share the same
    // User reference across factories, meaning MFA mutations in one test class leak into others.
    private readonly User _adminUser = User.Create(
        new Email(AdminEmail),
        BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: 4),
        UserRole.Administrator);

    private readonly Exam _activeExam;
    public Guid ActiveExamId => _activeExam.Id.Value;

    private string? _cachedToken;

    public TestWebApplicationFactory()
    {
        _activeExam = Exam.Create("Integration Test Exam", null, 50);
        _activeExam.Activate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Swap real repositories for fast in-memory fakes.
            services.RemoveAll<ICaptureRepository>();
            services.AddSingleton<ICaptureRepository, InMemoryCaptureRepository>();

            services.RemoveAll<IAuditLogRepository>();
            services.AddSingleton<IAuditLogRepository>(sp =>
                new InMemoryAuditLogRepository(sp.GetRequiredService<IServerSigningService>()));

            services.RemoveAll<IDeviceRepository>();
            services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();

            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(
                new InMemoryUserRepository(seed: [_adminUser]));

            // Replace real AlertService (makes HTTP calls) with a no-op fake for tests.
            services.RemoveAll<IAlertService>();
            services.AddSingleton<IAlertService, NullAlertService>();

            // Replace SignalR notification service (requires hub context) with a no-op for tests.
            services.RemoveAll<IRealtimeNotificationService>();
            services.AddSingleton<IRealtimeNotificationService, NullRealtimeNotificationService>();

            services.RemoveAll<IOcrResultRepository>();
            services.AddSingleton<IOcrResultRepository, InMemoryOcrResultRepository>();

            services.RemoveAll<IManualReviewRepository>();
            services.AddSingleton<IManualReviewRepository, InMemoryManualReviewRepository>();

            services.RemoveAll<IScoreRepository>();
            services.AddSingleton<IScoreRepository, InMemoryScoreRepository>();

            services.RemoveAll<ISecurityEventRepository>();
            services.AddSingleton<ISecurityEventRepository, InMemorySecurityEventRepository>();

            services.RemoveAll<IExamRepository>();
            services.AddSingleton<IExamRepository>(new InMemoryExamRepository(seed: [_activeExam]));

            services.RemoveAll<IReviewRequestRepository>();
            services.AddSingleton<IReviewRequestRepository, InMemoryReviewRequestRepository>();

            services.RemoveAll<IAnswerKeyRepository>();
            services.AddSingleton<IAnswerKeyRepository, InMemoryAnswerKeyRepository>();

            services.RemoveAll<IExamCandidateRepository>();
            services.AddSingleton<IExamCandidateRepository, InMemoryExamCandidateRepository>();

            services.RemoveAll<ISystemSettingsRepository>();
            services.AddSingleton<ISystemSettingsRepository, InMemorySystemSettingsRepository>();

            services.RemoveAll<IRefreshTokenRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();

            // IPasswordHasher and IJwtTokenService stay — real BCrypt + real JWT for auth tests.

            // Clear all health check registrations — no external services run in tests.
            services.Configure<HealthCheckServiceOptions>(o => o.Registrations.Clear());
        });
    }

    public async Task<string> GetAuthTokenAsync()
    {
        if (_cachedToken is not null)
            return _cachedToken;

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(AdminEmail, AdminPassword));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (body!.RequiresMfa || string.IsNullOrEmpty(body.Token))
            throw new InvalidOperationException(
                "Test admin user has MFA enabled — factory isolation is broken. Check for static shared user state.");
        _cachedToken = body.Token;
        return _cachedToken;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await GetAuthTokenAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
