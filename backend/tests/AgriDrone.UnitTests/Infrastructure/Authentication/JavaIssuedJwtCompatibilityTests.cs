using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AgriDrone.SharedInfrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Authentication;

public sealed class JavaIssuedJwtCompatibilityTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory,
        "contracts",
        "auth",
        "java-issued-access-token.v1.json");

    [Fact]
    public async Task JwksRetrieverLoadsTheJavaPublicSigningKey()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var jwks = fixture.RootElement.GetProperty("jwks").GetRawText();
        var retriever = new JwksConfigurationRetriever();

        var configuration = await retriever.GetConfigurationAsync(
            "https://auth.agridrone.test/.well-known/jwks.json",
            new StaticDocumentRetriever(jwks),
            CancellationToken.None);

        var key = Assert.Single(configuration.SigningKeys);
        Assert.Equal("java-contract-2026-01", key.KeyId);
    }

    [Fact]
    public void Be2ValidatesTheCanonicalJavaIssuedRs256Token()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var root = fixture.RootElement;
        var issuer = root.GetProperty("issuer").GetString()!;
        var audience = root.GetProperty("audience").GetString()!;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:JwksUri"] = $"{issuer}/.well-known/jwks.json",
                ["Jwt:RequireHttpsMetadata"] = "true"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        var bearerOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var validationParameters = bearerOptions.TokenValidationParameters.Clone();
        var jwks = root.GetProperty("jwks").GetRawText();
        validationParameters.IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys();

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        var principal = handler.ValidateToken(
            root.GetProperty("token").GetString(),
            validationParameters,
            out var validatedToken);
        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);
        var expectedClaims = root.GetProperty("expectedClaims");

        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Header.Alg);
        Assert.Equal("java-contract-2026-01", jwt.Header.Kid);
        Assert.Equal(
            expectedClaims.GetProperty("sub").GetString(),
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(
            expectedClaims.GetProperty("tenant_id").GetString(),
            principal.FindFirst(AgriDroneClaimTypes.TenantId)?.Value);
        Assert.Equal(
            expectedClaims.GetProperty("tenant_membership_id").GetString(),
            principal.FindFirst(AgriDroneClaimTypes.TenantMembershipId)?.Value);
        Assert.Equal(
            expectedClaims.GetProperty("tenant_role").GetString(),
            principal.FindFirst(AgriDroneClaimTypes.TenantRole)?.Value);
        Assert.True(principal.IsInRole("SYSTEM_ADMIN"));
        Assert.NotNull(bearerOptions.ConfigurationManager);
        Assert.Contains(
            SecurityAlgorithms.RsaSha256,
            validationParameters.ValidAlgorithms);
    }

    [Fact]
    public void Be2RejectsTheJavaTokenForTheWrongAudience()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var root = fixture.RootElement;
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = root.GetProperty("issuer").GetString(),
            ValidateAudience = true,
            ValidAudience = "wrong-audience",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = new JsonWebKeySet(
                    root.GetProperty("jwks").GetRawText())
                .GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        var exception = Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                root.GetProperty("token").GetString(),
                validationParameters,
                out _));

        Assert.NotNull(exception);
    }

    private sealed class StaticDocumentRetriever(string document) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(
            string address,
            CancellationToken cancel)
        {
            Assert.Equal(
                "https://auth.agridrone.test/.well-known/jwks.json",
                address);
            return Task.FromResult(document);
        }
    }
}
