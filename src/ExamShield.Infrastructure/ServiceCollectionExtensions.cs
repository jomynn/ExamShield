using ExamShield.Domain.Interfaces;
using ExamShield.Infrastructure.Alerts;
using ExamShield.Infrastructure.Cache;
using ExamShield.Infrastructure.OCR;
using ExamShield.Infrastructure.Persistence;
using ExamShield.Infrastructure.Persistence.Repositories;
using ExamShield.Infrastructure.Security;
using ExamShield.Infrastructure.Storage;
using ExamShield.Infrastructure.Watermark;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace ExamShield.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ExamShieldDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ICaptureRepository, CaptureRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<ISignatureVerificationService, EcdsaSignatureVerificationService>();
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
            services.AddSingleton<IObjectStore>(new MinioObjectStore(minioClient, storageOptions.BucketName));
            services.AddSingleton<IImageStorage, MinioImageStorage>();
        }
        else
        {
            services.AddSingleton<IImageStorage, InMemoryImageStorage>();
        }
        services.AddSingleton<IOcrService, StubOcrService>();
        services.AddScoped<IOcrResultRepository, OcrResultRepository>();
        services.AddScoped<IManualReviewRepository, ManualReviewRepository>();
        services.AddScoped<IScoreRepository, ScoreRepository>();
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

        services.AddHttpClient("Alerts");
        services.AddSingleton<IAlertService, AlertService>();
        return services;
    }
}
