using System.Security.Cryptography;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Security;

// Dev/test implementation: wraps the DEK using AES-256-GCM with the master key
// from configuration. Production should use Vault, KMS, or Azure Key Vault instead.
public sealed class ConfigKeyManagementService : IKeyManagementService
{
    private readonly byte[] _masterKey;

    public ConfigKeyManagementService(byte[] masterKey)
    {
        if (masterKey.Length != 32)
            throw new ArgumentException("Master key must be 32 bytes (AES-256).", nameof(masterKey));
        _masterKey = masterKey;
    }

    public Task<byte[]> WrapKeyAsync(byte[] plaintextDek, CancellationToken ct = default)
        => Task.FromResult(Wrap(plaintextDek));

    public Task<byte[]> UnwrapKeyAsync(byte[] wrappedDek, CancellationToken ct = default)
        => Task.FromResult(Unwrap(wrappedDek));

    // Wire format: [nonce (12)] [ciphertext (dek.len)] [tag (16)]
    private byte[] Wrap(byte[] dek)
    {
        var nonce  = new byte[12];
        var cipher = new byte[dek.Length];
        var tag    = new byte[16];
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_masterKey, 16);
        aes.Encrypt(nonce, dek, cipher, tag);
        return [..nonce, ..cipher, ..tag];
    }

    private byte[] Unwrap(byte[] wrapped)
    {
        const int overhead = 12 + 16;
        if (wrapped.Length <= overhead)
            throw new CryptographicException("Wrapped DEK is too short.");
        var nonce      = wrapped[..12];
        var ciphertext = wrapped[12..(wrapped.Length - 16)];
        var tag        = wrapped[^16..];
        var plaintext  = new byte[ciphertext.Length];
        using var aes  = new AesGcm(_masterKey, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
