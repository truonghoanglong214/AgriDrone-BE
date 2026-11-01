using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Missions;

public sealed class UploadMissionPlantGpsCsvRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}
