using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

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

            var usesJwks = !string.IsNullOrWhiteSpace(jwtOptions.JwksUri);
            var usesSharedSecret = !string.IsNullOrWhiteSpace(jwtOptions.Secret);

            if (usesJwks == usesSharedSecret)
            {
                throw new InvalidOperationException(
                    "Configure exactly one JWT signing-key source: Jwt:JwksUri or Jwt:Secret.");
            }

            services.Configure<JwtOptions>(
                configuration.GetSection(JwtOptions.SectionName));

            services
                .AddAuthentication(
                    JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = CreateValidationParameters(jwtOptions);

                    if (usesJwks)
                    {
                        options.ConfigurationManager =
                            new ConfigurationManager<OpenIdConnectConfiguration>(
                                jwtOptions.JwksUri,
                                new JwksConfigurationRetriever(),
                                new HttpDocumentRetriever
                                {
                                    RequireHttps = jwtOptions.RequireHttpsMetadata
                                });
                    }
                });

            services.AddAuthorization();

            return services;
        }

        private static TokenValidationParameters CreateValidationParameters(
            JwtOptions jwtOptions)
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = AgriDroneClaimTypes.SystemRole
            };

            if (!string.IsNullOrWhiteSpace(jwtOptions.JwksUri))
            {
                parameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
            }
            else
            {
                parameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.Secret));
                parameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            }

            return parameters;
        }
    }
}
