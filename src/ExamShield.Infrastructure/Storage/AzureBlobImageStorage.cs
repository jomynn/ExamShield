using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Storage;

// Azure Blob Storage adapter. Requires an Immutable Blob Storage policy
// (time-based retention) configured on the container at the portal/ARM level.
// Config keys:
//   Storage:BlobConnectionString   Azure Storage connection string or managed identity endpoint
//   Storage:BucketName             Container name
public sealed class AzureBlobImageStorage(BlobContainerClient container) : IImageStorage
{
    public async Task<string> StoreAsync(Guid captureId, byte[] imageBytes, CancellationToken ct = default)
    {
        var key    = $"captures/{captureId:N}";
        var blob   = container.GetBlobClient(key);
        var opts   = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
            Conditions  = new BlobRequestConditions { IfNoneMatch = Azure.ETag.All },
        };
        await blob.UploadAsync(new BinaryData(imageBytes), opts, ct);
        return key;
    }

    public async Task<byte[]> RetrieveAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var blob = container.GetBlobClient(storageKey);
            var resp = await blob.DownloadContentAsync(ct);
            return resp.Value.Content.ToArray();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new ImageNotFoundException(storageKey);
        }
    }
}
