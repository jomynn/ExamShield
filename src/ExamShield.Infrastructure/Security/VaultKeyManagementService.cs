using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Security;

// HashiCorp Vault Transit engine: wraps/unwraps the DEK via the Transit API.
// Requires: Vault address + a Vault token with transit/encrypt and transit/decrypt capabilities.
//
// Config keys:
//   Vault:Address      e.g. https://vault.internal:8200
//   Vault:Token        Vault service token
//   Vault:KeyName      Transit key name (default: "examshield-dek")
public sealed class VaultKeyManagementService : IKeyManagementService
{
    private readonly HttpClient _http;
    private readonly string _keyName;

    public VaultKeyManagementService(HttpClient http, string keyName)
    {
        _http    = http;
        _keyName = keyName;
    }

    public async Task<byte[]> WrapKeyAsync(byte[] plaintextDek, CancellationToken ct)
    {
        var b64 = Convert.ToBase64String(plaintextDek);
        var resp = await _http.PostAsJsonAsync(
            $"v1/transit/encrypt/{_keyName}",
            new { plaintext = b64 }, ct);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<VaultResponse>(ct)
            ?? throw new InvalidOperationException("Vault encrypt returned empty response.");
        return Convert.FromBase64String(doc.Data.Ciphertext.Split(':')[^1]);
    }

    public async Task<byte[]> UnwrapKeyAsync(byte[] wrappedDek, CancellationToken ct)
    {
        var ciphertext = $"vault:v1:{Convert.ToBase64String(wrappedDek)}";
        var resp = await _http.PostAsJsonAsync(
            $"v1/transit/decrypt/{_keyName}",
            new { ciphertext }, ct);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<VaultResponse>(ct)
            ?? throw new InvalidOperationException("Vault decrypt returned empty response.");
        return Convert.FromBase64String(doc.Data.Plaintext ?? "");
    }

    private sealed class VaultResponse
    {
        [JsonPropertyName("data")] public VaultData Data { get; init; } = new();
    }

    private sealed class VaultData
    {
        [JsonPropertyName("ciphertext")] public string  Ciphertext { get; init; } = "";
        [JsonPropertyName("plaintext")]  public string? Plaintext  { get; init; }
    }
}
