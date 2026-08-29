using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.SharedInfrastructure.Authentication;
using AgriDrone.Modules.Identity.Domain.Tenants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Authentication
{
    internal sealed class JwtTokenGenerator(
        IOptions<JwtOptions> options)
        : IJwtTokenGenerator
    {
        private readonly JwtOptions _options = options.Value;

        public AccessTokenResult GenerateAccessToken(
            AccessTokenRequest request)
        {
            var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                request.UserId.ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                request.Email),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

            if (request.TenantId is Guid tenantId &&
                request.TenantMembershipId is Guid membershipId &&
                request.TenantRole is TenantMemberRole tenantRole)
            {
                claims.AddRange(
                [
                    new Claim(
                        AgriDroneClaimTypes.TenantId,
                        tenantId.ToString()),
                    new Claim(
                        AgriDroneClaimTypes.TenantMembershipId,
                        membershipId.ToString()),
                    new Claim(
                        AgriDroneClaimTypes.TenantRole,
                        ToClaimValue(tenantRole))
                ]);
            }

            claims.AddRange(
                request.SystemRoles.Select(role =>
                    new Claim(AgriDroneClaimTypes.SystemRole, role)));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _options.Secret));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(
                _options.AccessTokenExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresAt.UtcDateTime,
                signingCredentials: credentials);

            var accessToken = new JwtSecurityTokenHandler()
                .WriteToken(token);

            return new AccessTokenResult(accessToken, expiresAt);
        }

        private static string ToClaimValue(
            TenantMemberRole role)
        {
            return role switch
            {
                TenantMemberRole.Owner => "OWNER",
                TenantMemberRole.TenantAdmin => "TENANT_ADMIN",
                TenantMemberRole.Member => "MEMBER",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            };
        }
    }
}
