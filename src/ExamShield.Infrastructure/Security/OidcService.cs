using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace ExamShield.Infrastructure.Security;

public sealed record OidcUserInfo(string Sub, string? Email, string? Name, string Provider);

public sealed class OidcService(IHttpClientFactory httpClientFactory, OidcOptions options)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Discovery> _discoveryCache = [];

    public OidcProviderConfig? GetProvider(string name)
        => options.Providers.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Enabled);

    // Returns the IdP authorization URL. state encodes returnUrl (sign in production).
    public async Task<string> BuildAuthorizationUrlAsync(
        OidcProviderConfig provider, string callbackUrl, string state, CancellationToken ct)
    {
        var discovery = await FetchDiscoveryAsync(provider.Authority, ct);
        var q = HttpUtility.ParseQueryString(string.Empty);
        q["response_type"] = "code";
        q["client_id"]     = provider.ClientId;
        q["redirect_uri"]  = callbackUrl;
        q["scope"]         = string.Join(" ", provider.Scopes);
        q["state"]         = state;
        return $"{discovery.AuthorizationEndpoint}?{q}";
    }

    // Exchanges the authorization code and returns the authenticated user's claims.
    public async Task<OidcUserInfo> ExchangeCodeAsync(
        OidcProviderConfig provider, string code, string callbackUrl, CancellationToken ct)
    {
        var discovery = await FetchDiscoveryAsync(provider.Authority, ct);
        var client = httpClientFactory.CreateClient("Oidc");

        var tokenResp = await client.PostAsync(discovery.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "authorization_code",
                ["code"]          = code,
                ["redirect_uri"]  = callbackUrl,
                ["client_id"]     = provider.ClientId,
                ["client_secret"] = provider.ClientSecret,
            }), ct);

        tokenResp.EnsureSuccessStatusCode();
        var tokenJson = await tokenResp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty token response from IdP.");

        // Prefer userinfo endpoint for fresh claims
        if (!string.IsNullOrWhiteSpace(discovery.UserinfoEndpoint)
            && !string.IsNullOrWhiteSpace(tokenJson.AccessToken))
        {
            var req = new HttpRequestMessage(HttpMethod.Get, discovery.UserinfoEndpoint);
            req.Headers.Authorization = new("Bearer", tokenJson.AccessToken);
            var uiResp = await client.SendAsync(req, ct);
            if (uiResp.IsSuccessStatusCode)
            {
                var ui = await uiResp.Content.ReadFromJsonAsync<UserinfoResponse>(JsonOpts, ct)
                    ?? throw new InvalidOperationException("Empty userinfo response.");
                return new OidcUserInfo(ui.Sub, ui.Email, ui.Name, provider.Name);
            }
        }

        // Fall back: parse id_token (tokens came directly from IdP over TLS, no sig validation needed)
        if (string.IsNullOrWhiteSpace(tokenJson.IdToken))
            throw new InvalidOperationException("IdP did not return an id_token and userinfo is unavailable.");

        return ParseIdToken(tokenJson.IdToken, provider.Name);
    }

    // ─── State helpers ───────────────────────────────────────────────────────

    public static string EncodeState(string returnUrl)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(returnUrl));

    public static string DecodeState(string state)
        => Encoding.UTF8.GetString(Convert.FromBase64String(state));

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<Discovery> FetchDiscoveryAsync(string authority, CancellationToken ct)
    {
        if (_discoveryCache.TryGetValue(authority, out var cached)) return cached;
        var client = httpClientFactory.CreateClient("Oidc");
        var url = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        var doc = await client.GetFromJsonAsync<Discovery>(url, JsonOpts, ct)
            ?? throw new InvalidOperationException($"Could not fetch OIDC discovery for {authority}.");
        _discoveryCache[authority] = doc;
        return doc;
    }

    private static OidcUserInfo ParseIdToken(string idToken, string provider)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(idToken))
            throw new InvalidOperationException("id_token is not a valid JWT.");
        var jwt   = handler.ReadJwtToken(idToken);
        var sub   = jwt.Subject ?? throw new InvalidOperationException("id_token missing sub claim.");
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var name  = jwt.Claims.FirstOrDefault(c => c.Type is "name" or "preferred_username")?.Value;
        return new OidcUserInfo(sub, email, name, provider);
    }

    // ─── Private DTOs ────────────────────────────────────────────────────────

    private sealed class Discovery
    {
        [JsonPropertyName("authorization_endpoint")] public string AuthorizationEndpoint { get; init; } = "";
        [JsonPropertyName("token_endpoint")]          public string TokenEndpoint          { get; init; } = "";
        [JsonPropertyName("userinfo_endpoint")]       public string? UserinfoEndpoint      { get; init; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("id_token")]     public string? IdToken     { get; init; }
    }

    private sealed class UserinfoResponse
    {
        [JsonPropertyName("sub")]   public string  Sub   { get; init; } = "";
        [JsonPropertyName("email")] public string? Email { get; init; }
        [JsonPropertyName("name")]  public string? Name  { get; init; }
    }
}
