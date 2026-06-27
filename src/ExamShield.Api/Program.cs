using System.Text;
using ExamShield.Api.Endpoints;
using ExamShield.Api.Hubs;
using ExamShield.Api.RateLimiting;
using ExamShield.Application.Behaviors;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExamShieldRateLimiting(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotificationService, SignalRNotificationService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<HashVerificationService>();
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterCaptureCommand).Assembly);
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
    });

// ── RBAC policies (hierarchical: higher roles include lower-level access) ─
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Student", p =>
        p.RequireRole("Student", "Operator", "Supervisor", "Auditor", "Administrator", "SecurityOfficer"));
    options.AddPolicy("Operator", p =>
        p.RequireRole("Operator", "Supervisor", "Auditor", "Administrator", "SecurityOfficer"));
    options.AddPolicy("Supervisor", p =>
        p.RequireRole("Supervisor", "Auditor", "Administrator", "SecurityOfficer"));
    options.AddPolicy("Auditor", p =>
        p.RequireRole("Auditor", "Administrator", "SecurityOfficer"));
    options.AddPolicy("Administrator", p =>
        p.RequireRole("Administrator", "SecurityOfficer"));
    options.AddPolicy("SecurityOfficer", p =>
        p.RequireRole("SecurityOfficer"));
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
        DuplicateScoreException      => (409, ex.Message),
        DuplicateOcrException        => (409, ex.Message),
        UserNotFoundException        => (404, ex.Message),
        UnauthorizedAccessException e => (403, e.Message),
        KeyNotFoundException e        => (404, e.Message),
        InvalidOperationException e  => (422, e.Message),
        ArgumentException e          => (400, e.Message),
        _                            => (500, "An unexpected error occurred.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { title, status });
}));

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
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

app.MapAuthEndpoints();
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
