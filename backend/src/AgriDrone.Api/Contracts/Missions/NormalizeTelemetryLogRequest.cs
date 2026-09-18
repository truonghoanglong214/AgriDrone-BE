using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.Api.Contracts.Missions;

public sealed class NormalizeTelemetryLogRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}