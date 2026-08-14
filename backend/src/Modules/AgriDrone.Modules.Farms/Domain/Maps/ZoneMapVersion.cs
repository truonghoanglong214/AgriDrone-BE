using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class ZoneMapVersion : AggregateRoot
{
    private ZoneMapVersion()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid ZoneId { get; private set; }

    public Guid? SourceMissionId { get; private set; }

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
}
