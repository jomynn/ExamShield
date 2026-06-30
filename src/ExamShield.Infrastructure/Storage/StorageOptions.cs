namespace ExamShield.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string Type { get; init; } = "InMemory";
    public string Endpoint { get; init; } = "localhost:9000";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = "examshield";
    public string AuditBucketName { get; init; } = "examshield-audit";
    public bool UseSSL { get; init; } = false;

    // AWS S3 / Azure Blob
    public string Region               { get; init; } = "us-east-1";
    public string BlobConnectionString { get; init; } = string.Empty;

    // Object Lock (WORM) — enables immutability at the storage layer.
    public bool EnableObjectLock { get; init; } = false;
    public int RetentionDays { get; init; } = 3650;  // 10 years
    public string RetentionMode { get; init; } = "COMPLIANCE";
}
