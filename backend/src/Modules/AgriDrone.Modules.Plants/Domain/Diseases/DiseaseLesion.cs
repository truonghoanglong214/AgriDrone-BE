using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Diseases;

public sealed class DiseaseLesion : Entity
{
    private DiseaseLesion()
    {
    }

    public Guid DiseaseDetectionId { get; private set; }

    public Guid MediaId { get; private set; }

    public decimal XMin { get; private set; }

    public decimal YMin { get; private set; }

    public decimal XMax { get; private set; }

    public decimal YMax { get; private set; }

    public decimal? Confidence { get; private set; }

    public decimal? AffectedRatio { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
