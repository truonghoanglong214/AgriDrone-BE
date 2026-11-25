using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.SharedInfrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Authentication;

internal sealed class TenantSelectionTokenService(
    IOptions<JwtOptions> options) : ITenantSelectionTokenService
{
    private const string TenantSelectionPurpose = "tenant_selection";
    private readonly JwtOptions _options = options.Value;

    public TenantSelectionTokenResult Generate(Guid userId, int authenticationVersion)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        }

        if (authenticationVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(authenticationVersion), "AuthenticationVersion must be >= 1.");
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(
            _options.TenantSelectionTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: GetAudience(),
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(
                    AgriDroneClaimTypes.TokenPurpose,
                    TenantSelectionPurpose),
                new Claim(
                    AgriDroneClaimTypes.AuthenticationVersion,
                    authenticationVersion.ToString(CultureInfo.InvariantCulture))
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                GetSigningKey(),
                SecurityAlgorithms.HmacSha256));

        return new TenantSelectionTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            new DateTimeOffset(token.ValidTo, TimeSpan.Zero));
    }

    public TenantSelectionValidationResult? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };

            var principal = tokenHandler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = GetAudience(),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GetSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub
                },
                out var validatedToken);

            var purpose = principal.FindFirst(
                AgriDroneClaimTypes.TokenPurpose)?.Value;
            var subject = principal.FindFirst(
                JwtRegisteredClaimNames.Sub)?.Value;
            var authVersionClaim = principal.FindFirst(
                AgriDroneClaimTypes.AuthenticationVersion)?.Value;

            if (purpose != TenantSelectionPurpose ||
                !Guid.TryParse(subject, out var userId) ||
                userId == Guid.Empty ||
                !int.TryParse(authVersionClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var authVersion) ||
                authVersion < 1)
            {
                return null;
            }

            return new TenantSelectionValidationResult(
                userId,
                authVersion,
                new DateTimeOffset(validatedToken.ValidTo, TimeSpan.Zero));
        }
        catch (Exception exception) when (
            exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private string GetAudience() =>
        $"{_options.Audience}.TenantSelection";

    private SymmetricSecurityKey GetSigningKey() =>
        new(Encoding.UTF8.GetBytes(_options.Secret));
}
