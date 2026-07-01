using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ExamShield.Infrastructure.Storage;

// Azure Blob Storage adapter for the IObjectStore contract.
// Immutability is enforced two ways:
//   1. IfNoneMatch = ETag.All on upload — prevents any overwrite of an existing blob.
//   2. Immutable Blob Storage (time-based WORM retention policy) must be configured
//      on the container in the Azure portal or via ARM before first use in production.
//      The SDK cannot enforce COMPLIANCE-mode retention on its own.
public sealed class AzureBlobObjectStore(BlobContainerClient container) : IObjectStore
{
    public async Task PutAsync(string key, byte[] data, CancellationToken ct)
    {
        var blob = container.GetBlobClient(key);
        var opts = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
            Conditions  = new BlobRequestConditions { IfNoneMatch = Azure.ETag.All },
        };
        await blob.UploadAsync(new BinaryData(data), opts, ct);
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken ct)
    {
        var blob = container.GetBlobClient(key);
        var resp = await blob.DownloadContentAsync(ct);
        return resp.Value.Content.ToArray();
    }
}
