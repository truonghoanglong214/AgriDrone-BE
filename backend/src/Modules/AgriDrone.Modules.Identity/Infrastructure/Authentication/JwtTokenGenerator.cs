using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.SharedInfrastructure.Authentication;
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

        public string GenerateAccessToken(
            Guid userId,
            string email,
            IEnumerable<string> roles)
        {
            var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                userId.ToString()),

            new(
                JwtRegisteredClaimNames.Email,
                email),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

            claims.AddRange(
                roles.Select(role =>
                    new Claim("role", role)));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _options.Secret));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    _options.AccessTokenExpirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
