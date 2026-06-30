using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Storage.Blobs;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Interfaces;
using ExamShield.Infrastructure.Alerts;
using ExamShield.Infrastructure.Cache;
using ExamShield.Infrastructure.Certificates;
using ExamShield.Infrastructure.Health;
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
        services.AddSingleton(storageOptions);
        if (storageOptions.Type == "MinIO")
        {
            var minioClient = new MinioClient()
                .WithEndpoint(storageOptions.Endpoint)
                .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
                .WithSSL(storageOptions.UseSSL)
                .Build();
            services.AddSingleton<IMinioClient>(minioClient);
            services.AddSingleton<IObjectStore>(new MinioObjectStore(minioClient, storageOptions.BucketName, storageOptions));
            services.AddSingleton<IImageStorage, MinioImageStorage>();
            services.AddHostedService<MinioBucketInitializer>();
        }
        else if (storageOptions.Type == "S3")
        {
            var s3 = new AmazonS3Client(RegionEndpoint.GetBySystemName(storageOptions.Region));
            services.AddSingleton<IAmazonS3>(s3);
            services.AddSingleton<IImageStorage>(new S3ImageStorage(s3, storageOptions));
        }
        else if (storageOptions.Type == "AzureBlob")
        {
            var container = new BlobContainerClient(storageOptions.BlobConnectionString, storageOptions.BucketName);
            services.AddSingleton<IImageStorage>(new AzureBlobImageStorage(container));
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
            }).AddHttpMessageHandler(() => new ExamShield.Infrastructure.Http.ResilienceDelegatingHandler());
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
        services.AddScoped<INotificationChannelSettingsRepository, NotificationChannelSettingsRepository>();
        services.AddScoped<IAnswerKeyRepository, AnswerKeyRepository>();
        services.AddScoped<IExamCandidateRepository, ExamCandidateRepository>();
        services.AddScoped<IDeviceCertificateRepository, DeviceCertificateRepository>();
        var wmOptions = configuration.GetSection(WatermarkOptions.Section).Get<WatermarkOptions>() ?? new WatermarkOptions();
        var hmacKey = string.IsNullOrEmpty(wmOptions.HmacKeyBase64)
            ? new byte[32]
            : Convert.FromBase64String(wmOptions.HmacKeyBase64);
        // LSB steganography is the production watermark; HmacWatermarkService remains available
        // for backward compatibility with captured images stored before the upgrade.
        services.AddSingleton<IWatermarkService, LsbSteganographyService>();
        services.AddSingleton(new HmacWatermarkService(hmacKey));
        services.AddSingleton<IQrStampService, QrStampService>();

        // Key Management Service — config-based in dev, swap for Vault/KMS/AKV in production.
        var kmsType = configuration["Kms:Type"] ?? "Config";
        if (kmsType == "Vault")
        {
            services.AddHttpClient("Vault", c =>
            {
                c.BaseAddress = new Uri(configuration["Vault:Address"] ?? "http://vault:8200");
                c.DefaultRequestHeaders.Add("X-Vault-Token", configuration["Vault:Token"] ?? "");
            }).AddHttpMessageHandler(() => new ExamShield.Infrastructure.Http.ResilienceDelegatingHandler());
            var keyName = configuration["Vault:KeyName"] ?? "examshield-dek";
            services.AddSingleton<IKeyManagementService>(sp =>
                new VaultKeyManagementService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("Vault"), keyName));
        }
        else if (kmsType == "AwsKms")
        {
            var kmsKeyId = configuration["Kms:KeyId"] ?? "";
            var awsKms   = new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(storageOptions.Region));
            services.AddSingleton<IAmazonKeyManagementService>(awsKms);
            services.AddSingleton<IKeyManagementService>(new AwsKmsKeyManagementService(awsKms, kmsKeyId));
        }
        else if (kmsType == "AzureKeyVault")
        {
            var keyId = configuration["KeyVault:KeyId"] ?? "";
            var cryptoClient = new CryptographyClient(new Uri(keyId), new DefaultAzureCredential());
            services.AddSingleton<IKeyManagementService>(new AzureKeyVaultKeyManagementService(cryptoClient));
        }
        else
        {
            // Default: config-based master key (dev / CI)
            var masterKeyB64 = configuration["Encryption:MasterKeyBase64"] ?? "";
            var masterKey = string.IsNullOrEmpty(masterKeyB64) ? new byte[32] : Convert.FromBase64String(masterKeyB64);
            services.AddSingleton<IKeyManagementService>(new ConfigKeyManagementService(masterKey));
        }
        var cacheOptions = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        if (cacheOptions.Type == "Redis")
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = cacheOptions.ConnectionString);
            // Redis-backed TOTP replay cache is safe across multiple API replicas.
            services.AddSingleton<ITotpUsedCodeCache, RedisTotpUsedCodeCache>();
        }
        else
        {
            services.AddDistributedMemoryCache();
            // In-memory TOTP cache — safe for single-replica dev/test only.
            services.AddSingleton<ITotpUsedCodeCache, InMemoryTotpUsedCodeCache>();
        }
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

        services.AddSingleton<IImageEncryptionService, AesGcmImageEncryptionService>();
        services.AddSingleton<ITotpService, TotpService>();
        // ITotpUsedCodeCache is registered above, conditionally: Redis in production, InMemory in dev.
        services.AddSingleton<IPasswordResetTokenRepository, InMemoryPasswordResetTokenRepository>();
        services.AddHttpClient("Alerts")
            .AddHttpMessageHandler(() => new ExamShield.Infrastructure.Http.ResilienceDelegatingHandler());
        services.AddHttpClient("Oidc")
            .AddHttpMessageHandler(() => new ExamShield.Infrastructure.Http.ResilienceDelegatingHandler());
        services.AddSingleton<IAlertService, AlertService>();
        var oidcOptions = configuration.GetSection(OidcOptions.Section).Get<OidcOptions>() ?? new OidcOptions();
        services.AddSingleton(oidcOptions);
        services.AddSingleton<OidcService>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IStudentCertificateService, QuestPdfCertificateService>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();

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
