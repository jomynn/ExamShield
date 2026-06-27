using ExamShield.Application.Interfaces;
using ExamShield.Domain.Interfaces;
using ExamShield.Infrastructure.Alerts;
using ExamShield.Infrastructure.Cache;
using ExamShield.Infrastructure.HealthChecks;
using ExamShield.Infrastructure.Messaging;
using ExamShield.Infrastructure.OCR;
using ExamShield.Infrastructure.Persistence;
using ExamShield.Infrastructure.Persistence.Repositories;
using ExamShield.Infrastructure.Security;
using ExamShield.Infrastructure.Storage;
using ExamShield.Infrastructure.Watermark;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using RabbitMQ.Client;

namespace ExamShield.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ExamShieldDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICaptureRepository, CaptureRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<ISignatureVerificationService, EcdsaSignatureVerificationService>();
        var serverKeyPem = configuration["ServerSigning:PrivateKeyPem"];
        services.AddSingleton<IServerSigningService>(new EcdsaServerSigningService(serverKeyPem));
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        var storageOptions = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        if (storageOptions.Type == "MinIO")
        {
            var minioClient = new MinioClient()
                .WithEndpoint(storageOptions.Endpoint)
                .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
                .WithSSL(storageOptions.UseSSL)
                .Build();
            services.AddSingleton<IMinioClient>(minioClient);
            services.AddSingleton(storageOptions);
            services.AddSingleton<IObjectStore>(new MinioObjectStore(minioClient, storageOptions.BucketName, storageOptions));
            services.AddSingleton<IImageStorage, MinioImageStorage>();
            services.AddHostedService<MinioBucketInitializer>();
        }
        else
        {
            services.AddSingleton<IImageStorage, InMemoryImageStorage>();
        }
        var ocrOptions = configuration.GetSection(OcrOptions.Section).Get<OcrOptions>() ?? new OcrOptions();
        if (ocrOptions.Type == "Http")
        {
            services.AddHttpClient<IOcrService, HttpOcrService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(ocrOptions.TimeoutSeconds);
            });
            services.AddSingleton(ocrOptions);
        }
        else
        {
            services.AddSingleton<IOcrService, StubOcrService>();
        }
        services.AddScoped<IOcrResultRepository, OcrResultRepository>();
        services.AddScoped<IManualReviewRepository, ManualReviewRepository>();
        services.AddScoped<IScoreRepository, ScoreRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<IExamRepository, ExamRepository>();
        services.AddScoped<IReviewRequestRepository, ReviewRequestRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddSingleton<IAnswerKeyRepository, StubAnswerKeyRepository>();
        var wmOptions = configuration.GetSection(WatermarkOptions.Section).Get<WatermarkOptions>() ?? new WatermarkOptions();
        var hmacKey = string.IsNullOrEmpty(wmOptions.HmacKeyBase64)
            ? new byte[32]
            : Convert.FromBase64String(wmOptions.HmacKeyBase64);
        services.AddSingleton<IWatermarkService>(new HmacWatermarkService(hmacKey));
        var cacheOptions = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        if (cacheOptions.Type == "Redis")
            services.AddStackExchangeRedisCache(o => o.Configuration = cacheOptions.ConnectionString);
        else
            services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, CacheService>();

        var msgOptions = configuration.GetSection(MessagingOptions.Section).Get<MessagingOptions>() ?? new MessagingOptions();
        if (msgOptions.Type == "RabbitMQ")
        {
            var factory = new ConnectionFactory
            {
                HostName = msgOptions.Host,
                Port = msgOptions.Port,
                UserName = msgOptions.Username,
                Password = msgOptions.Password,
                VirtualHost = msgOptions.VirtualHost
            };
            services.AddSingleton<IConnectionFactory>(factory);
            services.AddSingleton(msgOptions);
            services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
            services.AddHostedService<OcrConsumerService>();
        }
        else
        {
            services.AddScoped<IMessagePublisher, InMemoryMessagePublisher>(sp =>
                new InMemoryMessagePublisher(sp.GetRequiredService<ISender>()));
        }

        services.AddSingleton<ITotpService, TotpService>();
        services.AddHttpClient("Alerts");
        services.AddSingleton<IAlertService, AlertService>();

        var hc = services.AddHealthChecks()
            .AddCheck<DbContextHealthCheck>("postgres");
        if (cacheOptions.Type == "Redis")
            hc.AddCheck<RedisHealthCheck>("redis");
        if (msgOptions.Type == "RabbitMQ")
            hc.AddCheck<RabbitMqHealthCheck>("rabbitmq");
        if (storageOptions.Type == "MinIO")
            hc.AddCheck<MinioHealthCheck>("minio");

        return services;
    }
}
