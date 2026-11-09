using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Health;

public sealed class PlantHealthBoundaryException : AggregateRoot
{
    private PlantHealthBoundaryException() { }
    public Guid BatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public string SourceReferenceId { get; private set; } = null!;
    public Point OriginalPosition { get; private set; } = null!;
    public JsonDocument Original { get; private set; } = null!;
    public string Status { get; private set; } = "NEEDS_REVIEW";
    public uint Version { get; private set; }
    public static PlantHealthBoundaryException Create(Guid batchId, Guid tenantId, Guid farmId,
        Guid resultId, HealthBoundaryExceptionV4 source) => new()
    {
        Id = Guid.CreateVersion7(), BatchId = batchId, TenantId = tenantId, FarmId = farmId,
        SurveyResultId = resultId, SourceReferenceId = source.SourceReferenceId,
        OriginalPosition = new Point(source.OriginalPosition.Coordinates[0], source.OriginalPosition.Coordinates[1]) { SRID = 4326 },
        Original = JsonSerializer.SerializeToDocument(source)
    };
}
