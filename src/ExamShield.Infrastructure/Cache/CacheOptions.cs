namespace ExamShield.Infrastructure.Cache;

public sealed class CacheOptions
{
    public const string Section = "Cache";
    public string Type { get; init; } = "Memory";
    public string ConnectionString { get; init; } = "localhost:6379";
}
