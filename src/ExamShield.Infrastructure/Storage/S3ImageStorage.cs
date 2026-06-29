using Amazon.S3;
using Amazon.S3.Model;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Storage;

// AWS S3 storage adapter with Object Lock (COMPLIANCE mode for immutability).
// Config keys:
//   Storage:BucketName       e.g. examshield-captures
//   Storage:RetentionDays    days to retain (default 3650)
//   AWS credentials via the default credential chain
public sealed class S3ImageStorage(IAmazonS3 s3, StorageOptions options) : IImageStorage
{
    public async Task<string> StoreAsync(Guid captureId, byte[] imageBytes, CancellationToken ct = default)
    {
        var key = $"captures/{captureId:N}";
        var req = new PutObjectRequest
        {
            BucketName  = options.BucketName,
            Key         = key,
            InputStream = new MemoryStream(imageBytes),
            ContentType = "application/octet-stream",
        };

        if (options.EnableObjectLock)
        {
            req.ObjectLockMode            = ObjectLockMode.Compliance;
            req.ObjectLockRetainUntilDate = DateTime.UtcNow.AddDays(options.RetentionDays);
        }

        await s3.PutObjectAsync(req, ct);
        return key;
    }

    public async Task<byte[]> RetrieveAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var resp = await s3.GetObjectAsync(options.BucketName, storageKey, ct);
            using var ms = new MemoryStream();
            await resp.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ImageNotFoundException(storageKey);
        }
    }
}
