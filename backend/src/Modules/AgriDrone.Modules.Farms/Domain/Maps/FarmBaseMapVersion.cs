using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public enum FarmBaseMapStatus
{
    Draft,
    Published,
    Superseded
}

public sealed class FarmBaseMapVersion : AggregateRoot
{
    private FarmBaseMapVersion() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid SourceSurveyOrderId { get; private set; }
    public Guid? SourceMissionGroupId { get; private set; }
    public FarmBaseMapStatus Status { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ICollection<ZoneMapVersion> ZoneMapVersions { get; private set; } = [];

    public static FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        DomainGuard.Utc(createdAt);

        return new FarmBaseMapVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            FarmId = context.FarmId,
            VersionNumber = context.VersionNumber,
            SourceSurveyOrderId = context.SourceSurveyOrderId,
            SourceMissionGroupId = context.SourceMissionGroupId,
            Status = FarmBaseMapStatus.Draft,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(publishedBy);
        DomainGuard.Utc(publishedAt);

        if (Status != FarmBaseMapStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft farm base map can be published.");
        }

        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        Status = FarmBaseMapStatus.Published;
        UpdatedAt = publishedAt;
        Version++;
    }
}

public sealed record FarmBaseMapPublicationContext
{
    public FarmBaseMapPublicationContext(
        Guid sourceSurveyOrderId,
        Guid tenantId,
        Guid farmId,
        int versionNumber,
        Guid? sourceMissionGroupId)
    {
        DomainGuard.NotEmpty(sourceSurveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);

        if (sourceMissionGroupId.HasValue)
        {
            DomainGuard.NotEmpty(sourceMissionGroupId.Value);
        }

        SourceSurveyOrderId = sourceSurveyOrderId;
        TenantId = tenantId;
        FarmId = farmId;
        VersionNumber = versionNumber;
        SourceMissionGroupId = sourceMissionGroupId;
    }

    public Guid SourceSurveyOrderId { get; }
    public Guid TenantId { get; }
    public Guid FarmId { get; }
    public int VersionNumber { get; }
    public Guid? SourceMissionGroupId { get; }
}

public interface IFarmBaseMapPublicationService
{
    FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt);
}

public sealed class FarmBaseMapPublicationService : IFarmBaseMapPublicationService
{
    public FarmBaseMapVersion PrepareDraft(
        FarmBaseMapPublicationContext context,
        DateTimeOffset createdAt) =>
        FarmBaseMapVersion.PrepareDraft(context, createdAt);
}
