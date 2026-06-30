using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace ExamShield.Infrastructure.Storage;

public sealed class MinioBucketInitializer(
    IMinioClient client,
    StorageOptions options,
    ILogger<MinioBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await EnsureBucketAsync(options.BucketName, ct);
        await EnsureBucketAsync(options.AuditBucketName, ct);
    }

    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        bool exists;
        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(bucket);
            exists = await client.BucketExistsAsync(existsArgs, ct);
        }
        catch (Minio.Exceptions.AccessDeniedException)
        {
            logger.LogInformation("MinIO bucket '{Bucket}' already exists (access policy is private).", bucket);
            return;
        }

        if (exists) return;

        var makeArgs = new MakeBucketArgs().WithBucket(bucket);
        if (options.EnableObjectLock)
            makeArgs = makeArgs.WithObjectLock();

        await client.MakeBucketAsync(makeArgs, ct);
        logger.LogInformation("Created MinIO bucket '{Bucket}' (ObjectLock={Lock})", bucket, options.EnableObjectLock);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
