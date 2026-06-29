using System.Text;
using ExamShield.Api.Endpoints;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using ExamShield.Api.Hubs;
using ExamShield.Api.RateLimiting;
using System.Diagnostics;
using ExamShield.Application.Behaviors;
using ExamShield.Application.Commands.Login;
using ExamShield.Application.Queries.GetOcrResult;
using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Services;
using ExamShield.Infrastructure;
using ExamShield.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

// ── Serilog structured logging ────────────────────────────────────────────
// Bootstrap logger captures startup failures before full configuration loads.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Serilog is active in Development and Production only.
// In Testing, the default Microsoft.Extensions.Logging is used to avoid
// host-builder ordering conflicts with WebApplicationFactory.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        var isDev = ctx.HostingEnvironment.IsDevelopment();
        cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command",
                isDev ? LogEventLevel.Information : LogEventLevel.Warning)
            // Structured JSON to console — consumed by Grafana Loki, Datadog, etc.
            .WriteTo.Console(
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter());

        if (!isDev)
            cfg.WriteTo.File(
                path: "logs/examshield-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 100 * 1024 * 1024,
                rollOnFileSizeLimit: true);
    });
}

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173", "http://localhost:5174"];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddExamShieldRateLimiting(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotificationService, SignalRNotificationService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(new LoginOptions
{
    EnforceMfaForPrivilegedRoles =
        builder.Configuration.GetValue("Features:EnforceMfaForPrivilegedRoles", defaultValue: false)
});
builder.Services.AddSingleton<HashVerificationService>();
builder.Services.AddHostedService<DataSeeder>();
// Tracing activity source — registered as singleton so the same source is used across
// the application lifetime and can be subscribed to by any OTLP-compatible listener.
var activitySource = new System.Diagnostics.ActivitySource(
    ExamShield.Infrastructure.Telemetry.ExamShieldActivities.SourceName,
    ExamShield.Infrastructure.Telemetry.ExamShieldActivities.Version);
builder.Services.AddSingleton(activitySource);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterCaptureCommand).Assembly);
    cfg.AddOpenBehavior(typeof(TracingBehavior<,>));
    cfg.AddOpenBehavior(typeof(AlertBehavior<,>));
});

// ── JWT authentication ────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExamShield",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ExamShield",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // SignalR: browsers can't send Authorization headers over WebSocket/SSE,
        // so the client passes the token in the access_token query parameter.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// ── RBAC policies ─────────────────────────────────────────────────────────
// Each policy lists every role that satisfies it.
// Hierarchical: higher-privilege roles pass lower-privilege policies.
builder.Services.AddAuthorization(options =>
{
    // All authenticated users (including students, invigilators, etc.)
    var allRoles = new[]
    {
        "Student", "Operator", "Supervisor", "Auditor", "Administrator", "SecurityOfficer",
        "Invigilator", "OcrEngine", "ManualReviewer", "ReviewSupervisor", "ScoringEngine",
        "ResultPublisher", "ExamManager", "DeviceManager", "InvestigationOfficer",
        "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"
    };

    options.AddPolicy("Student", p => p.RequireRole(allRoles));

    // Field operations and above
    options.AddPolicy("Invigilator", p =>
        p.RequireRole("Invigilator", "Operator", "Supervisor", "Auditor", "Administrator",
            "SecurityOfficer", "ExamManager", "DeviceManager", "ManualReviewer",
            "ReviewSupervisor", "ScoringEngine", "ResultPublisher", "InvestigationOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    // Legacy Operator policy — kept for backward compatibility
    options.AddPolicy("Operator", p =>
        p.RequireRole("Operator", "Supervisor", "Auditor", "Administrator", "SecurityOfficer",
            "ExamManager", "DeviceManager", "ManualReviewer", "ReviewSupervisor", "ScoringEngine",
            "ResultPublisher", "InvestigationOfficer", "SuperAdministrator",
            "SecurityAdministrator", "SystemAdministrator"));

    // Pixel-level image access — strictly allowlisted, never includes admin roles.
    // Admin, Auditor, SecurityOfficer can see metadata/hashes but never raw image bytes.
    options.AddPolicy("ImageViewer", p =>
        p.RequireRole("Operator", "Invigilator", "Supervisor", "ManualReviewer",
            "ReviewSupervisor", "InvestigationOfficer"));

    // Review decisions
    options.AddPolicy("ManualReviewer", p =>
        p.RequireRole("ManualReviewer", "ReviewSupervisor", "Supervisor", "Administrator",
            "SecurityOfficer", "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    options.AddPolicy("ReviewSupervisor", p =>
        p.RequireRole("ReviewSupervisor", "Supervisor", "Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    // Result publication
    options.AddPolicy("ResultPublisher", p =>
        p.RequireRole("ResultPublisher", "Supervisor", "Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    // Exam & device management
    options.AddPolicy("ExamManager", p =>
        p.RequireRole("ExamManager", "Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    options.AddPolicy("DeviceManager", p =>
        p.RequireRole("DeviceManager", "Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    // Read-only oversight
    options.AddPolicy("Auditor", p =>
        p.RequireRole("Auditor", "Administrator", "SecurityOfficer", "InvestigationOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    options.AddPolicy("InvestigationOfficer", p =>
        p.RequireRole("InvestigationOfficer", "Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    // Admin tiers
    options.AddPolicy("Supervisor", p =>
        p.RequireRole("Supervisor", "Auditor", "Administrator", "SecurityOfficer",
            "ReviewSupervisor", "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    options.AddPolicy("Administrator", p =>
        p.RequireRole("Administrator", "SecurityOfficer",
            "SuperAdministrator", "SecurityAdministrator", "SystemAdministrator"));

    options.AddPolicy("SecurityOfficer", p =>
        p.RequireRole("SecurityOfficer", "SecurityAdministrator", "SuperAdministrator"));

    options.AddPolicy("SuperAdministrator", p =>
        p.RequireRole("SuperAdministrator"));
});

var app = builder.Build();

// ── Database migration (skipped in Testing — repositories are in-memory) ──
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ExamShieldDbContext>().Database.Migrate();
}

app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        CaptureNotFoundException     => (404, ex.Message),
        DeviceNotFoundException      => (404, ex.Message),
        DeviceNotApprovedException   => (422, ex.Message),
        DuplicateUploadException          => (409, ex.Message),
        DuplicateCaptureException         => (409, ex.Message),
        CaptureAlreadyTamperedException   => (409, ex.Message),
        AnswerKeyAlreadySetException     => (409, ex.Message),
        StudentAlreadyEnrolledException  => (409, ex.Message),
        ExamFullException                => (409, ex.Message),
        StudentHasCaptureException       => (409, ex.Message),
        ResultsAlreadyPublishedException => (409, ex.Message),
        NoScoresToPublishException       => (422, ex.Message),
        StudentNotEnrolledException      => (404, ex.Message),
        HashMismatchException        => (400, ex.Message),
        InvalidSignatureException    => (400, ex.Message),
        CaptureNotUploadedException  => (400, ex.Message),
        ExamNotActiveException       => (422, ex.Message),
        ExamExpiredException         => (422, ex.Message),
        OcrResultNotFoundException      => (404, ex.Message),
        ManualReviewNotFoundException   => (404, ex.Message),
        InvalidCredentialsException  => (401, ex.Message),
        UserAlreadyExistsException          => (409, ex.Message),
        DuplicateDevicePublicKeyException   => (409, ex.Message),
        DuplicateReviewRequestException     => (409, ex.Message),
        DuplicateScoreException      => (409, ex.Message),
        DuplicateOcrException        => (409, ex.Message),
        UserNotFoundException        => (404, ex.Message),
        UnauthorizedAccessException e => (403, e.Message),
        KeyNotFoundException e        => (404, e.Message),
        WatermarkTamperedException e => (422, e.Message),
        InvalidOperationException e  => (422, e.Message),
        ArgumentException e          => (400, e.Message),
        _                            => (500, "An unexpected error occurred.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { title, status });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts => opts
        .WithTitle("ExamShield API")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
}

app.UseHttpsRedirection();
app.UseMiddleware<ExamShield.Api.Middleware.SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false  // liveness: returns 200 if the process is alive, no dependency pings
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), error = e.Value.Exception?.Message })
        };
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
    }
});

app.MapSetupEndpoints();
app.MapAuthEndpoints();
app.MapExternalAuthEndpoints();
app.MapDeviceEndpoints();
app.MapCaptureEndpoints();
app.MapUploadEndpoints();
app.MapAuditEndpoints();
app.MapVerifyEndpoints();
app.MapOcrEndpoints();
app.MapScoreEndpoints();
app.MapManualReviewEndpoints();
app.MapSecurityEndpoints();
app.MapDashboardEndpoints();
app.MapPublicEndpoints();
app.MapExamEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapReportEndpoints();
app.MapSettingsEndpoints();
app.MapStudentEndpoints();
app.MapMfaEndpoints();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

public partial class Program;
