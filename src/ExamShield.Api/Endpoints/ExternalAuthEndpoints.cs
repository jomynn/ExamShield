using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ExamShield.Api.Endpoints;

public static class ExternalAuthEndpoints
{
    public static IEndpointRouteBuilder MapExternalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth/external").WithTags("Auth").AllowAnonymous();

        // GET /auth/external/{provider}/redirect?return_url={url}
        // Returns the IdP authorization URL for the SPA to redirect to.
        group.MapGet("/{provider}/redirect", async (
            string provider,
            [FromQuery(Name = "return_url")] string returnUrl,
            HttpRequest request,
            OidcService oidc,
            CancellationToken ct) =>
        {
            var cfg = oidc.GetProvider(provider);
            if (cfg is null) return Results.NotFound(new { error = $"Unknown or disabled provider '{provider}'." });

            var callbackUrl = BuildCallbackUrl(request, provider);
            var state       = OidcService.EncodeState(returnUrl);
            var authorizeUrl = await oidc.BuildAuthorizationUrlAsync(cfg, callbackUrl, state, ct);
            return Results.Ok(new { authorizeUrl });
        });

        // GET /auth/external/{provider}/callback?code=...&state=...
        // Called by the IdP after user consent. Issues our JWT and redirects to the SPA.
        group.MapGet("/{provider}/callback", async (
            string provider,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            HttpRequest request,
            OidcService oidc,
            IUserRepository users,
            IJwtTokenService jwt,
            IPasswordHasher hasher,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(error))
                return RedirectWithError(state, $"IdP error: {error}");

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return Results.BadRequest(new { error = "Missing code or state." });

            var cfg = oidc.GetProvider(provider);
            if (cfg is null) return Results.NotFound(new { error = $"Unknown provider '{provider}'." });

            string returnUrl;
            try { returnUrl = OidcService.DecodeState(state); }
            catch { return Results.BadRequest(new { error = "Invalid state parameter." }); }

            OidcUserInfo userInfo;
            try
            {
                var callbackUrl = BuildCallbackUrl(request, provider);
                userInfo = await oidc.ExchangeCodeAsync(cfg, code, callbackUrl, ct);
            }
            catch (Exception ex)
            {
                return RedirectWithError(returnUrl, ex.Message);
            }

            if (string.IsNullOrWhiteSpace(userInfo.Email))
                return RedirectWithError(returnUrl, "The identity provider did not share an email address.");

            var emailVo = new Email(userInfo.Email);
            var user = await users.FindByEmailAsync(emailVo, ct);
            if (user is null)
                return RedirectWithError(returnUrl, "No ExamShield account is associated with this identity. Contact your administrator.");

            if (!user.IsActive)
                return RedirectWithError(returnUrl, "Your account has been deactivated.");

            var accessToken = jwt.Generate(user);
            var redirectTarget = $"{returnUrl.TrimEnd('/')}?token={Uri.EscapeDataString(accessToken)}";
            return Results.Redirect(redirectTarget);
        });

        return app;
    }

    private static string BuildCallbackUrl(HttpRequest request, string provider)
    {
        var host = $"{request.Scheme}://{request.Host}";
        return $"{host}/auth/external/{provider}/callback";
    }

    private static IResult RedirectWithError(string? returnUrl, string message)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return Results.BadRequest(new { error = message });
        var url = $"{returnUrl.TrimEnd('/')}?oidc_error={Uri.EscapeDataString(message)}";
        return Results.Redirect(url);
    }
}
