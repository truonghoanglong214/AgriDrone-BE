using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Application.Provisioning;

public sealed record ProvisionTenantRequest(string Code, string Name);

public sealed record ProvisionedTenant(
    Guid TenantId,
    string Code,
    string Name,
    GeneralStatus Status,
    DateTimeOffset CreatedAt);

public interface ITenantProvisioningPort
{
    Task<Result<ProvisionedTenant>> ProvisionAsync(
        ProvisionTenantRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record InviteTenantOwnerRequest(
    Guid TenantId,
    Guid RequestedByUserId,
    string Email);

public sealed record InvitedTenantOwner(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);

public interface ITenantOwnerInvitationPort
{
    Task<Result<InvitedTenantOwner>> InviteAsync(
        InviteTenantOwnerRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class TenantProvisioningPort(
    ITenantRepository tenantRepository,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ITenantProvisioningPort
{
    private const string ActiveTenantCodeConstraint = "ux_tenants_code_active";

    public async Task<Result<ProvisionedTenant>> ProvisionAsync(
        ProvisionTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var existingTenant = await tenantRepository.GetByCodeAsync(code, cancellationToken);
        if (existingTenant is not null)
        {
            return Result.Failure<ProvisionedTenant>(TenantError.CodeAlreadyExists(code));
        }

        var tenant = Tenant.Create(
            code,
            name,
            GeneralStatus.Active,
            timeProvider.GetUtcNow());
        tenantRepository.Add(tenant);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(ActiveTenantCodeConstraint))
        {
            return Result.Failure<ProvisionedTenant>(TenantError.CodeAlreadyExists(code));
        }

        return Result.Success(new ProvisionedTenant(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            tenant.Status,
            tenant.CreatedAt));
    }
}

internal sealed class TenantOwnerInvitationPort(
    ITenantInvitationService invitationService) : ITenantOwnerInvitationPort
{
    public async Task<Result<InvitedTenantOwner>> InviteAsync(
        InviteTenantOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await invitationService.InviteAsync(
            new CreateTenantInvitationRequest(
                request.TenantId,
                request.RequestedByUserId,
                request.Email,
                TenantMemberRole.Owner,
                TenantInvitationPurpose.OwnerProvisioning),
            cancellationToken);

        return result.IsFailure
            ? Result.Failure<InvitedTenantOwner>(result.Error)
            : Result.Success(new InvitedTenantOwner(
                result.Value.InvitationId,
                result.Value.Email,
                result.Value.ExpiresAt));
    }
}
