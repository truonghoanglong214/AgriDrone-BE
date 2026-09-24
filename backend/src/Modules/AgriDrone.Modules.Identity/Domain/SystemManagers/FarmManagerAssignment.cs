using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.SystemManagers;

public sealed class FarmManagerAssignment : AggregateRoot
{
    private FarmManagerAssignment()
    {
    }

    private FarmManagerAssignment(
        Guid tenantId,
        Guid farmId,
        Guid systemManagerProfileId,
        Guid assignedBy,
        string reason,
        DateTimeOffset assignedAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        FarmId = farmId;
        SystemManagerProfileId = systemManagerProfileId;
        AssignedBy = assignedBy;
        AssignmentReason = reason.Trim();
        AssignedAt = assignedAt;
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid SystemManagerProfileId { get; private set; }

    public Guid AssignedBy { get; private set; }

    public string AssignmentReason { get; private set; } = null!;

    public DateTimeOffset AssignedAt { get; private set; }

    public Guid? EndedBy { get; private set; }

    public string? EndReason { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public long Version { get; private set; } = 1;

    public SystemManagerProfile SystemManagerProfile { get; private set; } = null!;

    public bool IsActive => !EndedAt.HasValue;

    public static FarmManagerAssignment Create(
        Guid tenantId,
        Guid farmId,
        Guid systemManagerProfileId,
        Guid assignedBy,
        string reason,
        DateTimeOffset assignedAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(systemManagerProfileId);
        DomainGuard.NotEmpty(assignedBy);
        DomainGuard.Utc(assignedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new FarmManagerAssignment(
            tenantId,
            farmId,
            systemManagerProfileId,
            assignedBy,
            reason,
            assignedAt);
    }

    public void End(
        Guid endedBy,
        string reason,
        DateTimeOffset endedAt,
        long expectedVersion)
    {
        DomainGuard.NotEmpty(endedBy);
        DomainGuard.Utc(endedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (expectedVersion != Version)
        {
            throw new InvalidOperationException(
                $"Farm manager assignment version conflict. Expected {expectedVersion}, current {Version}.");
        }

        if (!IsActive)
        {
            throw new InvalidOperationException("The farm manager assignment has already ended.");
        }

        if (endedAt < AssignedAt)
        {
            throw new ArgumentException(
                "EndedAt cannot precede AssignedAt.",
                nameof(endedAt));
        }

        EndedBy = endedBy;
        EndReason = reason.Trim();
        EndedAt = endedAt;
        Version++;
    }
}
