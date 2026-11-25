using AgriDrone.Modules.Identity.Infrastructure.Authentication;
using AgriDrone.SharedInfrastructure.Authentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Identity;

public sealed class TenantSelectionTokenServiceTests
{
    [Fact]
    public void GenerateThenValidateReturnsOriginalUserIdAuthVersionAndSignedExpiry()
    {
        var userId = Guid.NewGuid();
        var service = new TenantSelectionTokenService(
            Options.Create(
                new JwtOptions
                {
                    Issuer = "AgriDrone.UnitTests",
                    Audience = "AgriDrone.UnitTests.Clients",
                    Secret = "unit-test-secret-that-is-at-least-32-bytes-long",
                    TenantSelectionTokenExpirationMinutes = 5
                }));

        var token = service.Generate(userId, authenticationVersion: 3);

        var validated = service.Validate(token.Token);

        Assert.NotNull(validated);
        Assert.Equal(userId, validated.UserId);
        Assert.Equal(3, validated.AuthenticationVersion);
        Assert.Equal(token.ExpiresAt, validated.ExpiresAt);
    }
}
