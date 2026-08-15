namespace AgriDrone.SharedKernel.Application.Abstractions;

public interface ICurrentTenant
{
    Guid? TenantId { get; }

    Guid? MembershipId { get; }

    string? Role { get; }

    bool HasTenantContext { get; }
}
