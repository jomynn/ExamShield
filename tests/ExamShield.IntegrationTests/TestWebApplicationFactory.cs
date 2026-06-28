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

            services.RemoveAll<INotificationChannelSettingsRepository>();
            services.AddSingleton<INotificationChannelSettingsRepository, InMemoryNotificationChannelSettingsRepository>();

            services.RemoveAll<IRefreshTokenRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();

            services.RemoveAll<IPasswordResetTokenRepository>();
            services.AddSingleton<IPasswordResetTokenRepository, InMemoryPasswordResetTokenRepository>();

            services.RemoveAll<ITotpUsedCodeCache>();
            services.AddSingleton<ITotpUsedCodeCache, InMemoryTotpUsedCodeCache>();

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

    public async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        if (role == UserRole.Administrator)
            return await CreateAuthenticatedClientAsync();

        var email    = $"test-{role.ToString().ToLower()}@examshield.test";
        var password = "Test@1234!Role";

        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var existing = await users.FindByEmailAsync(new Email(email));
            if (existing is null)
            {
                var user = User.Create(
                    new Email(email),
                    BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
                    role);
                await users.SaveAsync(user);
            }
        }

        using var loginClient = CreateClient();
        var res  = await loginClient.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, password));
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    public async Task<string> RequestPasswordResetTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var repo  = scope.ServiceProvider.GetRequiredService<IPasswordResetTokenRepository>();
        var token = PasswordResetToken.Create(email);
        await repo.AddAsync(token);
        return token.Token;
    }

    public async Task<string> RequestExpiredPasswordResetTokenAsync(string email)
    {
        using var scope = Services.CreateScope();
        var repo  = scope.ServiceProvider.GetRequiredService<IPasswordResetTokenRepository>();
        var token = PasswordResetToken.Create(email, expiresAt: DateTimeOffset.UtcNow.AddHours(-1));
        await repo.AddAsync(token);
        return token.Token;
    }

    public async Task<Guid> CreateActivatedExamAsync(string name, int totalQuestions = 10)
    {
        var client  = await CreateAuthenticatedClientAsync();
        var examRes = await client.PostAsJsonAsync("/exams/",
            new CreateExamRequest(name, null, totalQuestions));
        var exam    = await examRes.Content.ReadFromJsonAsync<ExamResponse>();
        var examId  = exam!.ExamId;
        await client.PutAsync($"/exams/{examId}/activate", null);

        var key = Enumerable.Range(1, totalQuestions).ToDictionary(i => i, i => "A");
        await client.PostAsJsonAsync($"/exams/{examId}/answer-key", new SetAnswerKeyRequest(key));
        return examId;
    }

    public async Task<Guid> RegisterScoreForStudentAsync(Guid examId, int correctAnswers = 8)
    {
        using var scope    = Services.CreateScope();
        var scoreRepo      = scope.ServiceProvider.GetRequiredService<IScoreRepository>();
        var answerKeyRepo  = scope.ServiceProvider.GetRequiredService<IAnswerKeyRepository>();

        var answerKey = await answerKeyRepo.GetByExamIdAsync(new ExamId(examId));
        if (answerKey is null)
        {
            var defaultKey = Enumerable.Range(1, 10).ToDictionary(i => i, i => "A");
            answerKey      = new AnswerKey(defaultKey);
        }

        var total   = answerKey.Count;
        var correct = Math.Min(correctAnswers, total);
        var wrong   = total - correct;

        var answers = Enumerable.Range(1, correct)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(1.0)))
            .Concat(Enumerable.Range(correct + 1, wrong)
                .Select(i => new ExtractedAnswer(i, "B", new OcrConfidence(1.0))))
            .ToList<ExtractedAnswer>();

        var score = Score.Create(CaptureId.New(), new ExamId(examId), StudentId.New(), answers, answerKey);
        await scoreRepo.AddAsync(score);
        return score.Id.Value;
    }
}
