using Azure.Security.KeyVault.Keys.Cryptography;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Security;

// Azure Key Vault implementation: wraps/unwraps the DEK using an AKV key.
// Config keys:
//   KeyVault:KeyId   Full Key Vault key identifier URI
//   Authentication via DefaultAzureCredential (managed identity in prod, CLI in dev)
public sealed class AzureKeyVaultKeyManagementService(CryptographyClient cryptoClient)
    : IKeyManagementService
{
    public async Task<byte[]> WrapKeyAsync(byte[] plaintextDek, CancellationToken ct)
    {
        var result = await cryptoClient.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep, plaintextDek, ct);
        return result.EncryptedKey;
    }

    public async Task<byte[]> UnwrapKeyAsync(byte[] wrappedDek, CancellationToken ct)
    {
        var result = await cryptoClient.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep, wrappedDek, ct);
        return result.Key;
    }
}
