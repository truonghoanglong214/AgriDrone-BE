using AgriDrone.Modules.Identity.Domain.Roles;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class SystemRolesTests
{
    [Fact]
    public void AllContainsDistinctValidCodes()
    {
        var roles = SystemRoles.All.ToArray();

        Assert.NotEmpty(roles);
        Assert.Equal(
            roles.Length,
            roles.Select(role => role.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(roles, role =>
        {
            Assert.Matches("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", role.Code);
            Assert.False(string.IsNullOrWhiteSpace(role.Name));
            Assert.False(string.IsNullOrWhiteSpace(role.Description));
        });
        Assert.Contains(
            roles,
            role => role.Code == SystemRoles.SystemAdmin);
    }
}
