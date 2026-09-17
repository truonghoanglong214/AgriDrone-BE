using AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class AssignFarmMemberCommandValidatorTests
{
    private readonly AssignFarmMemberCommandValidator _validator = new();

    [Fact]
    public void ValidateAcceptsManagerWithAllZones()
    {
        var result = _validator.Validate(CreateCommand(
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            []));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAcceptsWorkerWithSelectedZones()
    {
        var result = _validator.Validate(CreateCommand(
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [Guid.NewGuid()]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsZoneIdsForAllZonesScope()
    {
        var result = _validator.Validate(CreateCommand(
            FarmMemberRole.Manager,
            FarmAccessScope.AllZones,
            [Guid.NewGuid()]));

        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(AssignFarmMemberCommand.ZoneIds));
    }

    [Fact]
    public void ValidateRejectsEmptyOrDuplicateSelectedZoneIds()
    {
        var zoneId = Guid.NewGuid();

        var emptyResult = _validator.Validate(CreateCommand(
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            []));
        var duplicateResult = _validator.Validate(CreateCommand(
            FarmMemberRole.Worker,
            FarmAccessScope.SelectedZones,
            [zoneId, zoneId]));

        Assert.False(emptyResult.IsValid);
        Assert.False(duplicateResult.IsValid);
    }

    private static AssignFarmMemberCommand CreateCommand(
        FarmMemberRole role,
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> zoneIds) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            accessScope,
            zoneIds,
            null,
            null);
}
