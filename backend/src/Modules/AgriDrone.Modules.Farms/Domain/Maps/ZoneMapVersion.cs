using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class ZoneMapVersion : AggregateRoot
{
    private ZoneMapVersion()
    {
    }

    private ZoneMapVersion(
        Guid id,
        Guid farmId,
        Guid zoneId,
        Guid sourceMissionId,
        Guid sourceApprovalId,
        int versionNumber,
        decimal gridBearingDeg,
        decimal rowSpacingM,
        decimal plantSpacingM,
        string algorithmVersion,
        JsonDocument parameters,
        DateTimeOffset createdAt)
    {
        Id = id;
        FarmId = farmId;
        ZoneId = zoneId;
        SourceMissionId = sourceMissionId;
        SourceApprovalId = sourceApprovalId;
        VersionNumber = versionNumber;
        Status = MapVersionStatus.Draft;
        GridBearingDeg = gridBearingDeg;
        RowSpacingM = rowSpacingM;
        PlantSpacingM = plantSpacingM;
        AlgorithmVersion = algorithmVersion;
        Parameters = parameters;
        CreatedAt = createdAt;
    }

    public Guid FarmId { get; private set; }

    public Guid ZoneId { get; private set; }

    public Guid? SourceMissionId { get; private set; }

    public Guid? SourceApprovalId { get; private set; }

    public int VersionNumber { get; private set; }

    public MapVersionStatus Status { get; private set; }

    public decimal? GridBearingDeg { get; private set; }

    public decimal? RowSpacingM { get; private set; }

    public decimal? PlantSpacingM { get; private set; }

    public string? AlgorithmVersion { get; private set; }

    public JsonDocument Parameters { get; private set; } = null!;

    public Guid? ConfirmedBy { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public FarmZone Zone { get; private set; } = null!;

    public static ZoneMapVersion CreateDraft(
        Guid id,
        Guid farmId,
        Guid zoneId,
        Guid sourceMissionId,
        Guid sourceApprovalId,
        int versionNumber,
        decimal gridBearingDeg,
        decimal rowSpacingM,
        decimal plantSpacingM,
        string algorithmVersion,
        JsonDocument parameters,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(sourceMissionId);
        DomainGuard.NotEmpty(sourceApprovalId);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(gridBearingDeg);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            gridBearingDeg,
            360);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rowSpacingM, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(plantSpacingM, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmVersion);
        ArgumentNullException.ThrowIfNull(parameters);
        DomainGuard.Utc(createdAt);

        return new ZoneMapVersion(
            id,
            farmId,
            zoneId,
            sourceMissionId,
            sourceApprovalId,
            versionNumber,
            gridBearingDeg,
            rowSpacingM,
            plantSpacingM,
            algorithmVersion.Trim(),
            JsonDocument.Parse(parameters.RootElement.GetRawText()),
            createdAt);
    }

    public void Confirm(Guid actorId, DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(actorId);
        DomainGuard.Utc(confirmedAt);

        if (Status != MapVersionStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Only a draft map version can be confirmed; current status is '{Status}'.");
        }

        Status = MapVersionStatus.Confirmed;
        ConfirmedBy = actorId;
        ConfirmedAt = confirmedAt;
    }

    public void Supersede()
    {
        if (Status != MapVersionStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Only a confirmed map version can be superseded; current status is '{Status}'.");
        }

        Status = MapVersionStatus.Superseded;
    }

}
