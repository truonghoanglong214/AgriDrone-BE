using AgriDrone.Modules.Identity.Infrastructure.Authentication;
using AgriDrone.SharedInfrastructure.Authentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Identity;

public sealed class TenantSelectionTokenServiceTests
{
    [Fact]
    public void GenerateThenValidateReturnsOriginalUserId()
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

        var token = service.Generate(userId);

        var validatedUserId = service.Validate(token.Token);

        Assert.Equal(userId, validatedUserId);
    }
}
