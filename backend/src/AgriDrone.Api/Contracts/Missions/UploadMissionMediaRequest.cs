using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Missions;

public sealed class UploadMissionMediaRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(20)]
    public List<IFormFile> Files { get; init; } = [];
}
