using Amazon.S3;
using Amazon.S3.Model;

namespace ExamShield.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, string bucketName, StorageOptions options) : IObjectStore
{
    public async Task PutAsync(string key, byte[] data, CancellationToken ct)
    {
        var req = new PutObjectRequest
        {
            BucketName  = bucketName,
            Key         = key,
            InputStream = new MemoryStream(data),
            ContentType = "application/json",
        };
        if (options.EnableObjectLock)
        {
            req.ObjectLockMode            = ObjectLockMode.Compliance;
            req.ObjectLockRetainUntilDate = DateTime.UtcNow.AddDays(options.RetentionDays);
        }
        await client.PutObjectAsync(req, ct);
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken ct)
    {
        var resp = await client.GetObjectAsync(bucketName, key, ct);
        using var ms = new MemoryStream();
        await resp.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
