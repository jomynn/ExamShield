namespace ExamShield.Infrastructure.Security;

public sealed class OidcOptions
{
    public const string Section = "Oidc";
    public OidcProviderConfig[] Providers { get; init; } = [];
}

public sealed class OidcProviderConfig
{
    public string Name        { get; init; } = "";
    public string Authority   { get; init; } = "";
    public string ClientId    { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string[] Scopes   { get; init; } = ["openid", "email", "profile"];
    public bool Enabled       { get; init; }
}
