using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Infrastructure.Configuration;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Identity;

public sealed class SystemAdminBootstrapOptionsValidatorTests
{
    private readonly SystemAdminBootstrapOptionsValidator _validator = new();

    [Fact]
    public void ValidateAllowsEmptyValuesWhenBootstrapIsDisabled()
    {
        var result = _validator.Validate(
            null,
            new SystemAdminBootstrapOptions
            {
                Enabled = false
            });

        Assert.False(result.Failed);
    }

    [Fact]
    public void ValidateAcceptsValidEnabledConfiguration()
    {
        var result = _validator.Validate(
            null,
            new SystemAdminBootstrapOptions
            {
                Enabled = true,
                Email = "admin@example.com",
                FullName = "System Administrator"
            });

        Assert.False(result.Failed);
    }

    [Fact]
    public void ValidateRejectsDisplayAddressAndBlankName()
    {
        var result = _validator.Validate(
            null,
            new SystemAdminBootstrapOptions
            {
                Enabled = true,
                Email = "Admin <admin@example.com>",
                FullName = " "
            });

        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures.Count());
    }
}
