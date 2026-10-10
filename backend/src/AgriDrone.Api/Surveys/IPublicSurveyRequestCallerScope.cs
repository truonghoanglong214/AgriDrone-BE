using Microsoft.AspNetCore.DataProtection;

namespace AgriDrone.Api.Surveys;

public interface IPublicSurveyRequestCallerScope
{
    string GetOrCreate(HttpContext httpContext);
}

internal sealed class PublicSurveyRequestCallerScope(
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : IPublicSurveyRequestCallerScope
{
    internal const string CookieName = "AgriDrone.PublicSurveySession";
    private const string Purpose =
        "AgriDrone.PublicSurveyRequests.CallerScope.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    private readonly ITimeLimitedDataProtector _protector =
        dataProtectionProvider
            .CreateProtector(Purpose)
            .ToTimeLimitedDataProtector();

    public string GetOrCreate(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var sessionId = TryReadSessionId(httpContext.Request.Cookies)
            ?? CreateSession(httpContext);
        return $"public-session:{sessionId:N}";
    }

    private Guid? TryReadSessionId(IRequestCookieCollection cookies)
    {
        if (!cookies.TryGetValue(CookieName, out var protectedValue) ||
            string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            var value = _protector.Unprotect(
                protectedValue,
                out var expiresAt);
            return expiresAt > timeProvider.GetUtcNow() &&
                   Guid.TryParseExact(value, "N", out var sessionId) &&
                   sessionId != Guid.Empty
                ? sessionId
                : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private Guid CreateSession(HttpContext httpContext)
    {
        var sessionId = Guid.CreateVersion7();
        var expiresAt = timeProvider.GetUtcNow().Add(Lifetime);
        var protectedValue = _protector.Protect(
            sessionId.ToString("N"),
            Lifetime);
        httpContext.Response.Cookies.Append(
            CookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                Expires = expiresAt
            });
        return sessionId;
    }
}
