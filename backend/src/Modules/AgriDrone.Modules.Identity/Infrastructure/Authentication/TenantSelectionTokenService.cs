using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.SharedInfrastructure.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Authentication;

internal sealed class TenantSelectionTokenService(
    IOptions<JwtOptions> options) : ITenantSelectionTokenService
{
    private const string TenantSelectionPurpose = "tenant_selection";
    private readonly JwtOptions _options = options.Value;

    public TenantSelectionTokenResult Generate(Guid userId)
    {
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
                    TenantSelectionPurpose)
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                GetSigningKey(),
                SecurityAlgorithms.HmacSha256));

        return new TenantSelectionTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    public Guid? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = GetAudience(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GetSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Sub
                },
                out _);

            var purpose = principal.FindFirst(
                AgriDroneClaimTypes.TokenPurpose)?.Value;
            var subject = principal.FindFirst(
                JwtRegisteredClaimNames.Sub)?.Value;

            return purpose == TenantSelectionPurpose &&
                   Guid.TryParse(subject, out var userId)
                ? userId
                : null;
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
