using AgriDrone.SharedKernel.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AgriDrone.SharedInfrastructure.Authentication
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            var jwtOptions =
                configuration
                    .GetSection(JwtOptions.SectionName)
                    .Get<JwtOptions>()
                ?? throw new InvalidOperationException(
                    "JWT configuration is missing.");

            if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
            {
                throw new InvalidOperationException(
                    "JWT secret is missing.");
            }

            services.Configure<JwtOptions>(
                configuration.GetSection(JwtOptions.SectionName));

            services.AddHttpContextAccessor();

            services.AddScoped<ICurrentUser, CurrentUser>();

            services
                .AddAuthentication(
                    JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwtOptions.Issuer,

                            ValidateAudience = true,
                            ValidAudience = jwtOptions.Audience,

                            ValidateLifetime = true,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey =
                                new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(
                                        jwtOptions.Secret)),

                            NameClaimType =
                                JwtRegisteredClaimNames.Sub,

                            RoleClaimType = "role"
                        };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
