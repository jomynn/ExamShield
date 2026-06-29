namespace ExamShield.Domain.Interfaces;

// Wraps (encrypts) and unwraps (decrypts) a Data Encryption Key (DEK).
// Implementations: config-based (dev), Vault Transit, AWS KMS, Azure Key Vault.
public interface IKeyManagementService
{
    Task<byte[]> WrapKeyAsync(byte[] plaintextDek, CancellationToken ct = default);
    Task<byte[]> UnwrapKeyAsync(byte[] wrappedDek, CancellationToken ct = default);
}
