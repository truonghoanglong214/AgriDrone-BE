using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Identity;

public sealed class TenantMembershipTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ChangeRolePromotesMemberToTenantAdmin()
    {
        var membership = CreateMembership(TenantMemberRole.Member);

        membership.ChangeRole(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.TenantAdmin, membership.Role);
    }

    [Fact]
    public void ChangeRoleDemotesTenantAdminToMember()
    {
        var membership = CreateMembership(TenantMemberRole.TenantAdmin);

        membership.ChangeRole(TenantMemberRole.Member);

        Assert.Equal(TenantMemberRole.Member, membership.Role);
    }

    [Fact]
    public void ChangeRoleDoesNotAllowDemotingOwner()
    {
        var membership = CreateMembership(TenantMemberRole.Owner);

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(TenantMemberRole.Member));
    }

    [Fact]
    public void ChangeRoleDoesNotAllowAssigningOwner()
    {
        var membership = CreateMembership(TenantMemberRole.Member);

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(TenantMemberRole.Owner));
    }

    [Fact]
    public void ChangeRoleDoesNotAllowDemotingOwnerToTenantAdmin()
    {
        var membership = CreateMembership(TenantMemberRole.Owner);

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(TenantMemberRole.TenantAdmin));
    }

    [Fact]
    public void ChangeRoleDoesNotAllowPromotingTenantAdminToOwner()
    {
        var membership = CreateMembership(TenantMemberRole.TenantAdmin);

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(TenantMemberRole.Owner));
    }

    [Fact]
    public void DeactivateDoesNotAllowDeactivatingOwner()
    {
        var membership = CreateMembership(TenantMemberRole.Owner);

        Assert.Throws<InvalidOperationException>(() =>
            membership.Deactivate(Now));
    }

    [Fact]
    public void DeactivateAllowsDeactivatingMember()
    {
        var membership = CreateMembership(TenantMemberRole.Member);

        membership.Deactivate(Now);

        Assert.Equal(GeneralStatus.Inactive, membership.Status);
    }

    [Fact]
    public void RelinquishOwnershipDemotesOwnerToTenantAdmin()
    {
        var membership = CreateMembership(TenantMemberRole.Owner);

        membership.RelinquishOwnership(TenantMemberRole.TenantAdmin);

        Assert.Equal(TenantMemberRole.TenantAdmin, membership.Role);
    }

    [Fact]
    public void RelinquishOwnershipRejectsNonOwner()
    {
        var membership = CreateMembership(TenantMemberRole.Member);

        Assert.Throws<InvalidOperationException>(() =>
            membership.RelinquishOwnership(
                TenantMemberRole.TenantAdmin));
    }

    [Fact]
    public void RelinquishOwnershipRejectsOwnerAsNewRole()
    {
        var membership = CreateMembership(TenantMemberRole.Owner);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            membership.RelinquishOwnership(TenantMemberRole.Owner));
    }

    [Fact]
    public void AssumeOwnershipPromotesActiveMember()
    {
        var membership = CreateMembership(TenantMemberRole.Member);

        membership.AssumeOwnership();

        Assert.Equal(TenantMemberRole.Owner, membership.Role);
    }

    [Fact]
    public void AssumeOwnershipRejectsInactiveMembership()
    {
        var membership = CreateMembership(TenantMemberRole.Member);
        membership.Deactivate(Now);

        Assert.Throws<InvalidOperationException>(
            membership.AssumeOwnership);
    }

    private static TenantMembership CreateMembership(
        TenantMemberRole role) =>
        TenantMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            GeneralStatus.Active,
            Now,
            Now);
}
