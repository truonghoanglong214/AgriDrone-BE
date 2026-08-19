using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class EffectiveAccessService(IdentityDbContext dbContext)
    : IEffectiveAccessService
{
    public async Task<AccessDecision> CheckTenantAsync(
        Guid actorId,
        Guid tenantId,
        TenantAccessLevel requiredAccess,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested tenant access level is not supported.");
        }

        var accessState = await GetTenantAccessStateAsync(
            actorId,
            tenantId,
            cancellationToken);

        var tenantStateDecision = ValidateTenantAccessState(accessState);
        if (!tenantStateDecision.IsAllowed)
        {
            return tenantStateDecision;
        }

        return HasRequiredTenantAccess(accessState!.Role, requiredAccess)
            ? AccessDecision.Allow()
            : AccessDecision.Deny(AccessDenialReason.TenantRoleInsufficient);
    }

    public async Task<AccessDecision> CheckFarmAsync(
        Guid actorId,
        Guid tenantId,
        Guid farmId,
        FarmAccessLevel requiredAccess,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested farm access level is not supported.");
        }

        var tenantAccessState = await GetTenantAccessStateAsync(
            actorId,
            tenantId,
            cancellationToken);

        var tenantStateDecision = ValidateTenantAccessState(
            tenantAccessState);
        if (!tenantStateDecision.IsAllowed)
        {
            return tenantStateDecision;
        }

        if (tenantAccessState!.Role == TenantMemberRole.Owner)
        {
            return AccessDecision.Allow();
        }

        var farmAccessState = await GetFarmAccessStateAsync(
            actorId,
            tenantId,
            farmId,
            cancellationToken);

        return ValidateFarmAccessState(farmAccessState, requiredAccess);
    }

    public async Task<AccessDecision> CheckZoneAsync(
        Guid actorId,
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        FarmAccessLevel requiredAccess,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(zoneId, Guid.Empty);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested Zone access level is not supported.");
        }

        var tenantAccessState = await GetTenantAccessStateAsync(
            actorId,
            tenantId,
            cancellationToken);

        var tenantStateDecision = ValidateTenantAccessState(
            tenantAccessState);
        if (!tenantStateDecision.IsAllowed)
        {
            return tenantStateDecision;
        }

        if (tenantAccessState!.Role == TenantMemberRole.Owner)
        {
            return AccessDecision.Allow();
        }

        var farmAccessState = await GetFarmAccessStateAsync(
            actorId,
            tenantId,
            farmId,
            cancellationToken);

        var farmStateDecision = ValidateFarmAccessState(
            farmAccessState,
            requiredAccess);
        if (!farmStateDecision.IsAllowed)
        {
            return farmStateDecision;
        }

        if (farmAccessState!.AccessScope == FarmAccessScope.AllZones)
        {
            return AccessDecision.Allow();
        }

        if (farmAccessState.AccessScope != FarmAccessScope.SelectedZones)
        {
            return AccessDecision.Deny(
                AccessDenialReason.FarmAccessScopeUnsupported);
        }

        var hasActiveAssignment = await dbContext.ZoneAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment =>
                    assignment.FarmMembershipId == farmAccessState.Id &&
                    assignment.FarmId == farmId &&
                    assignment.ZoneId == zoneId &&
                    assignment.RevokedAt == null,
                cancellationToken);

        return hasActiveAssignment
            ? AccessDecision.Allow()
            : AccessDecision.Deny(
                AccessDenialReason.ZoneAssignmentNotActive);
    }

    private Task<TenantAccessState?> GetTenantAccessStateAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == actorId &&
                membership.TenantId == tenantId)
            .Select(membership => new TenantAccessState(
                membership.Role,
                membership.Status,
                membership.Tenant.Status,
                membership.Tenant.DeletedAt,
                membership.User.Status,
                membership.User.DeletedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<FarmAccessState?> GetFarmAccessStateAsync(
        Guid actorId,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken) =>
        dbContext.FarmMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == actorId &&
                membership.TenantId == tenantId &&
                membership.FarmId == farmId)
            .Select(membership => new FarmAccessState(
                membership.Id,
                membership.Role,
                membership.AccessScope,
                membership.Status))
            .SingleOrDefaultAsync(cancellationToken);

    private static AccessDecision ValidateTenantAccessState(
        TenantAccessState? accessState)
    {
        if (accessState is null)
        {
            return AccessDecision.Deny(
                AccessDenialReason.TenantMembershipNotFound);
        }

        if (accessState.UserStatus != UserStatus.Active ||
            accessState.UserDeletedAt is not null)
        {
            return AccessDecision.Deny(AccessDenialReason.UserInactive);
        }

        if (accessState.TenantStatus != GeneralStatus.Active ||
            accessState.TenantDeletedAt is not null)
        {
            return AccessDecision.Deny(AccessDenialReason.TenantInactive);
        }

        return accessState.MembershipStatus == GeneralStatus.Active
            ? AccessDecision.Allow()
            : AccessDecision.Deny(
                AccessDenialReason.TenantMembershipInactive);
    }

    private static AccessDecision ValidateFarmAccessState(
        FarmAccessState? accessState,
        FarmAccessLevel requiredAccess)
    {
        if (accessState is null)
        {
            return AccessDecision.Deny(
                AccessDenialReason.FarmMembershipNotFound);
        }

        if (accessState.Status != GeneralStatus.Active)
        {
            return AccessDecision.Deny(
                AccessDenialReason.FarmMembershipInactive);
        }

        return HasRequiredFarmAccess(accessState.Role, requiredAccess)
            ? AccessDecision.Allow()
            : AccessDecision.Deny(AccessDenialReason.FarmRoleInsufficient);
    }

    private static bool HasRequiredTenantAccess(
        TenantMemberRole actualRole,
        TenantAccessLevel requiredAccess) =>
        requiredAccess switch
        {
            TenantAccessLevel.Member => actualRole is
                TenantMemberRole.Member or
                TenantMemberRole.TenantAdmin or
                TenantMemberRole.Owner,
            TenantAccessLevel.Admin => actualRole is
                TenantMemberRole.TenantAdmin or
                TenantMemberRole.Owner,
            TenantAccessLevel.Owner => actualRole == TenantMemberRole.Owner,
            _ => false
        };

    private static bool HasRequiredFarmAccess(
        FarmMemberRole actualRole,
        FarmAccessLevel requiredAccess) =>
        requiredAccess switch
        {
            FarmAccessLevel.Member => actualRole is
                FarmMemberRole.Worker or FarmMemberRole.Manager,
            FarmAccessLevel.Manager => actualRole == FarmMemberRole.Manager,
            _ => false
        };

    private sealed record TenantAccessState(
        TenantMemberRole Role,
        GeneralStatus MembershipStatus,
        GeneralStatus TenantStatus,
        DateTimeOffset? TenantDeletedAt,
        UserStatus UserStatus,
        DateTimeOffset? UserDeletedAt);

    private sealed record FarmAccessState(
        Guid Id,
        FarmMemberRole Role,
        FarmAccessScope AccessScope,
        GeneralStatus Status);
}
