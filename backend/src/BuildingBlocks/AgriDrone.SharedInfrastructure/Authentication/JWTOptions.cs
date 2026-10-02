using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedInfrastructure.Authentication
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; init; } = string.Empty;

        public string Audience { get; init; } = string.Empty;

        public string Secret { get; init; } = string.Empty;

        public string JwksUri { get; init; } = string.Empty;

        public bool RequireHttpsMetadata { get; init; } = true;

        public int AccessTokenExpirationMinutes { get; init; }

        public int TenantSelectionTokenExpirationMinutes { get; init; } = 5;
    }
}
