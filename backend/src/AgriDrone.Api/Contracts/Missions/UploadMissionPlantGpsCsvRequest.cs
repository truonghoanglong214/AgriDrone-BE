using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Missions;

public sealed class UploadMissionPlantGpsCsvRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    public Guid ClientOperationId { get; set; }

    public Guid ModelVersionId { get; set; }

    public int SourceVersion { get; set; }

    public double GridBearingDeg { get; set; }

    public double RowSpacingM { get; set; }

    public double PlantSpacingM { get; set; }

    [Required]
    public string AlgorithmVersion { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }
}
