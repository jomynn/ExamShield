using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Security;

// AWS KMS implementation: wraps/unwraps the DEK using a Customer Master Key (CMK).
// Config keys:
//   Kms:KeyId     ARN or alias of the CMK, e.g. "alias/examshield-dek"
//   AWS credentials resolved via the default credential chain (env vars / instance profile)
public sealed class AwsKmsKeyManagementService(IAmazonKeyManagementService kms, string keyId)
    : IKeyManagementService
{
    public async Task<byte[]> WrapKeyAsync(byte[] plaintextDek, CancellationToken ct)
    {
        var req = new EncryptRequest
        {
            KeyId     = keyId,
            Plaintext = new MemoryStream(plaintextDek),
        };
        var resp = await kms.EncryptAsync(req, ct);
        return resp.CiphertextBlob.ToArray();
    }

    public async Task<byte[]> UnwrapKeyAsync(byte[] wrappedDek, CancellationToken ct)
    {
        var req = new DecryptRequest
        {
            KeyId            = keyId,
            CiphertextBlob   = new MemoryStream(wrappedDek),
        };
        var resp = await kms.DecryptAsync(req, ct);
        return resp.Plaintext.ToArray();
    }
}
